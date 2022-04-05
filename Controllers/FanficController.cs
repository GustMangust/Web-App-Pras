﻿using CourceProject.Data.Repository;
using CourceProject.Models;
using CourceProject.Utility;
using CourceProject.ViewModel;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.AspNet.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourceProject.Controllers
{
    public class FanficController : Controller
    {
        private readonly IRepository ctx;
        private readonly Microsoft.AspNetCore.Identity.UserManager<IdentityUser> _userManager;
        private readonly IHostingEnvironment hostingEnv;
        public FanficController(IRepository repo, Microsoft.AspNetCore.Identity.UserManager<IdentityUser> userManager, IHostingEnvironment env)
        {
            _userManager = userManager;
            ctx = repo;
            hostingEnv = env;
        }
        [HttpGet]
        public IActionResult AddFanfic()
        {
            if (TempData["UserId"] != null)
            {
                ViewBag.UserId = TempData["UserId"].ToString();
            }
            ViewData["Id"] = new SelectList(ctx.GetAllFandoms(), "Id", "Name");
            return View();
        }
        [HttpGet]
        public IActionResult AddChapter(int id)
        {
            Chapter chapter = new Chapter { Fanfic_Id = id };
            return View(chapter);
        }
        [HttpGet]
        public IActionResult GetFanficsByTag(int tagId)
        {
            var fanfics = new List<Fanfic>();
            foreach (var fanficTag in ctx.GetFanficTags().Where(x => x.TagId == tagId))
            {
                fanfics.Add(ctx.GetFanfic(fanficTag.FanficId));
            }
            (List<Fanfic>, List<Fandom>) data = (fanfics, ctx.GetAllFandoms());
            return View(data);
        }
        [HttpGet]
        public IActionResult FanficDetails(int id)
        {
            int sum = 0;
            foreach (Rating rating in ctx.GetFanficRatings(id))
            {
                sum += rating.Mark;
            }
            decimal averageRating = 0;
            try
            {
                averageRating = Math.Round((decimal)sum / (decimal)ctx.GetFanficRatings(id).Count, 2);
            }
            catch
            {
                averageRating = 0;
            }
            (Fanfic, Fandom, List<Chapter>, List<Comment>, List<IdentityUser>, decimal) tuple = (ctx.GetFanfic(id),
                                                                                                ctx.GetFandom(ctx.GetFanfic(id).Fandom_Id),
                                                                                                ctx.GetChapters(id), ctx.GetFanficComments(id),
                                                                                                _userManager.Users.ToList(),
                                                                                                averageRating);
            return View(tuple);
        }
        [HttpGet]
        public IActionResult ChapterDetails(int chapterId)
        {
            (Chapter, int) data = (ctx.GetChapter(chapterId), ctx.GetChapterLikes(chapterId).Count);
            ViewBag.Id = ctx.GetFanfic(ctx.GetChapter(chapterId).Fanfic_Id).User_Id;
            return View(data);
        }
        [HttpGet]
        public IActionResult EditFanfic(int id)
        {
            var fanfic = ctx.GetFanfic(id);
            if (string.IsNullOrEmpty(fanfic.Description))
            {
                fanfic.Description = "";
            }
            ViewBag.Fanfic = fanfic;
            var stringBuilder = new StringBuilder();
            foreach (FanficTag tag in ctx.GetFanficTags().Where(x => x.FanficId == id))
            {
                stringBuilder.Append(ctx.GetTag(tag.TagId).Name + " ");
            }
            ViewBag.Tags = stringBuilder.ToString();
            ViewData["Id"] = new SelectList(ctx.GetAllFandoms(), "Id", "Name");
            return View();
        }
        [HttpGet]
        public IActionResult EditChapter(int id)
        {
            ViewBag.LocalUrl = ctx.GetChapter(id).LocalUrl;
            return View(ctx.GetChapter(id));
        }
        [HttpGet]
        public IActionResult AllFanfics()
        {
            if (ctx.GetPreferences(User.Identity.GetUserId()).Count == 0 && User.IsInRole("User"))
            {
                return RedirectToAction("SetPreferences", "Account");
            }
            var tagCloud = new Dictionary<Tag, string>();
            foreach (var fanficTag in ctx.GetFanficTags())
            {
                string tagClass = "tag1";
                if (ctx.GetFanficTags().Where(x => x.TagId == fanficTag.TagId).ToList().Count <= 0)
                {
                    tagClass = "tag1";
                }
                else if (ctx.GetFanficTags().Where(x => x.TagId == fanficTag.TagId).ToList().Count <= 1)
                {
                    tagClass = "tag2";
                }
                else if (ctx.GetFanficTags().Where(x => x.TagId == fanficTag.TagId).ToList().Count <= 2)
                {
                    tagClass = "tag3";
                }
                else if (ctx.GetFanficTags().Where(x => x.TagId == fanficTag.TagId).ToList().Count <= 3)
                {
                    tagClass = "tag4";
                }
                else if (ctx.GetFanficTags().Where(x => x.TagId == fanficTag.TagId).ToList().Count <= 4)
                {
                    tagClass = "tag5";
                }
                if (tagCloud.FirstOrDefault(x => x.Key.Id == fanficTag.TagId).Equals(new KeyValuePair<Tag, string>()))
                {
                    tagCloud.Add(ctx.GetAllTags().FirstOrDefault(x => x.Id == fanficTag.TagId), tagClass);
                }
            }
            ViewBag.Tags = tagCloud.Distinct();
            var fanfics = ctx.GetAllFanfics();
            var allFanficRatings = new Dictionary<Fanfic, decimal>();
            foreach (var fan in fanfics)
            {
                try
                {
                    allFanficRatings.Add(fan, Math.Round((decimal)ctx.GetFanficRatings(fan.Id).Sum(x => x.Mark) / (decimal)ctx.GetFanficRatings(fan.Id).Count, 2));
                }
                catch
                {
                    allFanficRatings.Add(fan, 0);
                }
            }
            if (User.IsInRole("User"))
            {
                fanfics.Clear();
                var userFanficRatings = new Dictionary<Fanfic, decimal>();
                foreach (var fanficPair in allFanficRatings)
                {
                    foreach (var pref in ctx.GetPreferences(User.Identity.GetUserId()))
                    {
                        if (pref.FandomId == fanficPair.Key.Fandom_Id)
                        {
                            userFanficRatings.Add(fanficPair.Key, fanficPair.Value);
                        }
                    }
                }
                ViewBag.Fanfics = userFanficRatings;
            }
            else
            {
                ViewBag.Fanfics = allFanficRatings;
            }
            ViewBag.Fandoms = ctx.GetAllFandoms();
            return View();
        }
        [HttpGet]
        public IActionResult UserBookmarks()
        {
            var fanfics = new List<Fanfic>();
            foreach (var b in ctx.GetBookmarks(User.Identity.GetUserId()))
            {
                var fanfic = ctx.GetFanfic(b.FanficId);
                if (fanfic != null)
                {
                    fanfics.Add(fanfic);
                }
            }
            (List<Fanfic>, List<Fandom>) data = (fanfics, ctx.GetAllFandoms());
            return View(data);
        }
        [HttpGet]
        public IActionResult UserFanfics(string sortBy = "")
        {
            (List<Fanfic> Fanfics, List<Fandom> Fandoms) tuple;
            switch (sortBy)
            {
                case "titleAsc":
                    tuple = (ctx.GetUserFanfics(User.Identity.GetUserId()).OrderBy(x => x.Title).ToList(), ctx.GetAllFandoms());
                    break;
                case "titleDesc":
                    tuple = (ctx.GetUserFanfics(User.Identity.GetUserId()).OrderByDescending(x => x.Title).ToList(), ctx.GetAllFandoms());
                    break;
                default:
                    tuple = (ctx.GetUserFanfics(User.Identity.GetUserId()), ctx.GetAllFandoms());
                    break;
            }
            return View(tuple);
        }
        [HttpGet]
        public IActionResult ChapterNavigate(int chapterId, string param)
        {
            var currentChapter = ctx.GetChapter(chapterId);
            var chapter = new Chapter();
            if (param == "Back")
            {
                chapter = ctx.GetChapters(currentChapter.Fanfic_Id).FirstOrDefault(x => x.Number == currentChapter.Number - 1);
            }
            else
            {
                chapter = ctx.GetChapters(currentChapter.Fanfic_Id).FirstOrDefault(x => x.Number == currentChapter.Number + 1);
            }
            if (chapter == null)
            {
                return RedirectToAction("ChapterDetails", "Fanfic", new { chapterId = chapterId });
            }
            return RedirectToAction("ChapterDetails", "Fanfic", new { chapterId = chapter.Id });
        }
        [HttpGet]
        public IActionResult SearchResult(string idList)
        {
            if (string.IsNullOrEmpty(idList))
            {
                idList = "";
            }
            var fanfics = new List<Fanfic>();
            foreach (string str in idList.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                fanfics.Add(ctx.GetFanfic(Convert.ToInt32(str)));
            }
            var data = (fanfics, ctx.GetAllFandoms());
            return View(data);
        }
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {

            try
            {
                var filePath = "";
                if (file.Length > 0)
                {
                    var folderRoot = Path.Combine(hostingEnv.ContentRootPath, "Uploads");
                    filePath = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    filePath = Path.Combine(folderRoot, filePath);
                    await using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);
                }
                return Ok(new { success = true, message = "File Uploaded", fileName = filePath });
            }
            catch (Exception)
            {
                return BadRequest(new { success = false, message = "Error file failed to upload" });
            }

        }
        [HttpPost]
        public async Task<ActionResult> AddLike(int chapterId, string userId)
        {
            ctx.AddLike(new Like { ChapterId = chapterId, UserId = userId });
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("ChapterDetails", "Fanfic", new { chapterId = chapterId });
            }
            return Content("Fail");
        }
        [HttpPost]
        public ActionResult Search(string text)
        {
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
            var basePath = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
            var indexPath = Path.Combine(basePath, "index");
            using var dir = FSDirectory.Open(indexPath);
            var analyzer = new StandardAnalyzer(AppLuceneVersion);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
            using var writer = new IndexWriter(dir, indexConfig);
            writer.DeleteAll();
            var fandoms = ctx.GetAllFandoms();
            var chapters = ctx.GetAllChapters();
            var comments = ctx.GetAllComments();
            foreach (Fanfic fanfic in ctx.GetAllFanfics())
            {
                var info = new StringBuilder($"{fanfic.Title + " " + fanfic.Description + " " + fandoms.FirstOrDefault(x => x.Id == fanfic.Fandom_Id).Name}");
                foreach (var chapter in chapters.Where(x => x.Fanfic_Id == fanfic.Id))
                {
                    info.Append(" " + chapter.Title + " " + chapter.Body);
                }
                foreach (var comment in comments.Where(x => x.Fanfic_Id == fanfic.Id))
                {
                    info.Append(" " + comment.Body);
                }
                var doc = new Document
                {
                    new StringField("FanficId",
                        fanfic.Id.ToString(),
                        Field.Store.YES),
                    new TextField("AllBoundInformation",
                        info.ToString(),
                        Field.Store.YES)
                };
                writer.AddDocument(doc);
            }
            writer.Flush(triggerMerge: false, applyAllDeletes: false);
            var phrase = new MultiPhraseQuery();
            foreach (string word in text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                phrase.Add(new Term("AllBoundInformation", word.ToLower()));
            }
            using var reader = writer.GetReader(applyAllDeletes: true);
            var searcher = new IndexSearcher(reader);
            var hits = searcher.Search(phrase, 20).ScoreDocs;
            var idList = new StringBuilder();
            foreach (var hit in hits)
            {
                var foundDoc = searcher.Doc(hit.Doc);
                idList.Append(foundDoc.Get("FanficId") + " ");
            }

            return RedirectToAction("SearchResult", "Fanfic", new { idList = idList.ToString() });
        }
        [HttpPost]
        public async Task<ActionResult> AddComment(int fanficId, string text)
        {
            ctx.AddComment(new Comment { Fanfic_Id = fanficId, Body = text, User_Id = User.Identity.GetUserId() });
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("FanficDetails", "Fanfic", new { id = fanficId });
            }
            return Content("Fail");
        }
        [HttpPost]
        public async Task<ActionResult> AddRating(int fanficId, string mark)
        {
            ctx.AddRating(new Rating { FanficId = fanficId, Mark = Convert.ToInt32(mark), UserId = User.Identity.GetUserId() });
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("FanficDetails", "Fanfic", new { id = fanficId });
            }
            return Content("Fail");
        }
        [HttpPost]
        public async Task<ActionResult> RemoveChapter(int id)
        {
            var removedChapter = ctx.GetChapter(id);
            var list = ctx.GetChapters(removedChapter.Fanfic_Id);
            for (int i = 0; i < list.Count; i++)
            {
                if (i > list.IndexOf(removedChapter))
                {
                    list[i].Number--;
                    ctx.UpdateChapter(list[i]);
                }
            }
            ctx.RemoveChapter(id);
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("FanficDetails", "Fanfic", new { id = removedChapter.Fanfic_Id });
            }
            return Content("Fail");
        }
        [HttpPost]
        public async Task<IActionResult> EditFanfic(EditFanficViewModel fanficVM)
        {
            var fanfic = ctx.GetFanfic(fanficVM.Id);
            fanfic.Id = fanficVM.Id;
            fanfic.Title = fanficVM.Title;
            fanfic.Description = fanficVM.Description;
            fanfic.Fandom_Id = fanficVM.FandomId;
            fanfic.User_Id = fanficVM.UserId;
            ctx.UpdateFanfic(fanfic);

            var newTags = new List<string>();
            if (!string.IsNullOrEmpty(fanficVM.Tags))
            {
                newTags = fanficVM.Tags.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            }
            foreach (var fanficTag in ctx.GetFanficTags().Where(x => x.FanficId == fanficVM.Id))
            {
                if (!newTags.Contains(ctx.GetTag(fanficTag.TagId).Name))
                {
                    ctx.RemoveFanficTag(fanficTag.Id);
                }
            }
            await ctx.SaveChangesAsync();
            foreach (string tag in newTags)
            {
                var tagFound = ctx.GetAllTags().FirstOrDefault(x => x.Name == tag);
                if (tagFound == null)
                {
                    ctx.AddTag(new Tag { Name = tag });
                    await ctx.SaveChangesAsync();
                    ctx.AddFanficTag(new FanficTag { FanficId = fanficVM.Id, TagId = ctx.GetTagByName(tag).Id });
                    await ctx.SaveChangesAsync();
                    continue;
                }
                if (ctx.GetFanficTags().FirstOrDefault(x => x.FanficId == fanficVM.Id && x.TagId == ctx.GetTagByName(tag).Id) == null)
                {
                    ctx.AddFanficTag(new FanficTag { FanficId = fanficVM.Id, TagId = ctx.GetTagByName(tag).Id });
                    await ctx.SaveChangesAsync();
                }
            }
            await ctx.SaveChangesAsync();
            return RedirectToAction("FanficDetails", "Fanfic", new { id = fanfic.Id });
        }
        [HttpPost]
        public async Task<IActionResult> ChapterUp(int id)
        {
            var chapter = ctx.GetChapter(id);
            var previousChapter = ctx.GetAllChapters().FirstOrDefault(x => x.Number == chapter.Number - 1 && x.Fanfic_Id == chapter.Fanfic_Id);
            if (previousChapter != null)
            {
                previousChapter.Number = chapter.Number;
                chapter.Number -= 1;
                ctx.UpdateChapter(previousChapter);
                ctx.UpdateChapter(chapter);
                if (await ctx.SaveChangesAsync())
                {
                    return RedirectToAction("FanficDetails", "Fanfic", new { id = chapter.Fanfic_Id });
                }
                return Content("Fail");
            }
            else
            {
                return RedirectToAction("FanficDetails", "Fanfic", new { id = chapter.Fanfic_Id });
            }
        }
        public async Task<IActionResult> ChapterDown(int id)
        {
            var chapter = ctx.GetChapter(id);
            var previousChapter = ctx.GetAllChapters().FirstOrDefault(x => x.Number == chapter.Number + 1 && x.Fanfic_Id == chapter.Fanfic_Id);
            if (previousChapter != null)
            {
                previousChapter.Number = chapter.Number;
                chapter.Number += 1;
                ctx.UpdateChapter(previousChapter);
                ctx.UpdateChapter(chapter);
                if (await ctx.SaveChangesAsync())
                {
                    return RedirectToAction("FanficDetails", "Fanfic", new { id = chapter.Fanfic_Id });
                }
                return Content("Fail");
            }
            else
            {
                return RedirectToAction("FanficDetails", "Fanfic", new { id = chapter.Fanfic_Id });
            }
        }
        [HttpPost]
        public async Task<IActionResult> EditChapter(Chapter chapter)
        {
            chapter.ImageUrl = ImageManagement.EditImageToChapter(chapter);
            ctx.UpdateChapter(chapter);
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("FanficDetails", "Fanfic", new { id = chapter.Fanfic_Id });
            }
            return Content("Fail");
        }
        [HttpPost]
        public async Task<IActionResult> AddChapter(Chapter c)
        {
            var chapter = new Chapter { Title = c.Title, Body = c.Body, Fanfic_Id = c.Fanfic_Id, Number = ctx.GetAllChapters().Where(x => x.Fanfic_Id == c.Fanfic_Id).ToList().Count + 1, ImageUrl = ImageManagement.AddImageToChapter(c), LocalUrl = c.LocalUrl };
            ctx.AddChapter(chapter);
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("FanficDetails", "Fanfic", new { id = c.Fanfic_Id });
            }
            return Content("Fail");
        }
        [HttpPost]
        public async Task<IActionResult> AddFanfic(AddFanficViewModel fanfic, string userId)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(userId))
                {

                    userId = User.Identity.GetUserId();
                }

                var fan = new Fanfic { Title = fanfic.Title, Description = fanfic.Description, Fandom_Id = fanfic.FandomId, User_Id = userId };
                ctx.AddFanfic(fan);
                await ctx.SaveChangesAsync();
                foreach (string tag in fanfic.Tags.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var tagFound = ctx.GetAllTags().FirstOrDefault(x => x.Name == tag);
                    if (tagFound == null)
                    {
                        ctx.AddTag(new Tag { Name = tag });
                        await ctx.SaveChangesAsync();
                    }
                    int fanficId = ctx.GetUserFanfics(userId).FirstOrDefault(x => x.Title == fanfic.Title && x.Description == fanfic.Description && x.Fandom_Id == fanfic.FandomId).Id;
                    ctx.AddFanficTag(new FanficTag { FanficId = fanficId, TagId = ctx.GetTagByName(tag).Id });
                }
            }
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("UserFanfics", "Fanfic");
            }
            return RedirectToAction("Index", "Home");
        }
        [HttpPost]
        public async Task<IActionResult> AddFanficToFavourite(int fanficId)
        {
            var fanfic = ctx.GetFanfic(fanficId);
            if (fanfic != null)
            {
                ctx.AddBookmark(new Bookmark { FanficId = fanficId, UserId = User.Identity.GetUserId() });
            }
            await ctx.SaveChangesAsync();
            return RedirectToAction("UserBookmarks", "Fanfic");
        }
        [HttpPost]
        public async Task<IActionResult> RemoveBookmark(int fanficId)
        {
            var fanfic = ctx.GetFanfic(fanficId);
            var bookmark = ctx.GetBookmark(User.Identity.GetUserId(), fanficId);
            if (fanfic != null && bookmark != null)
            {
                ctx.RemoveBookmark(bookmark.Id);
            }
            if (await ctx.SaveChangesAsync())
            {
                return RedirectToAction("UserBookmarks", "Fanfic");
            }
            return RedirectToAction("UserBookmarks", "Fanfic");
        }
    }
}