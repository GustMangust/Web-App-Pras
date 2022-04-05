using CourceProject.Data.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CourceProject.Controllers
{
    public class AdminController : Controller
    {

        private readonly UserManager<IdentityUser> _userManager;

        private readonly SignInManager<IdentityUser> _signInManager;

        public AdminController(UserManager<IdentityUser> userManager,
                                      SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }
        [HttpGet]
        public IActionResult AdminPage()
        {
            return View(_userManager.Users.ToList());
        }
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {

            var user = await _userManager.FindByIdAsync(userId);

            await _userManager.RemoveFromRoleAsync(user, "User");

            await _userManager.RemoveFromRoleAsync(user, "Admin");

            await _userManager.DeleteAsync(user);

            return RedirectToAction("AdminPage");
        }
        [HttpPost]
        public async Task<IActionResult> BlockUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            await _userManager.SetLockoutEndDateAsync(user, new DateTime().AddYears(5000));
            return RedirectToAction("AdminPage");
        }
        [HttpPost]
        public async Task<IActionResult> UnblockUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            await _userManager.SetLockoutEndDateAsync(user, null);
            return RedirectToAction("AdminPage");
        }
        [HttpPost]
        public async Task<IActionResult> GiveAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            await _userManager.AddToRoleAsync(user, "Admin");
            await _signInManager.SignInAsync(user, false);
            return RedirectToAction("AdminPage");
        }
        [HttpGet]
        public IActionResult AddFanficAdmin(string userId)
        {
            TempData["UserId"] = userId;
            return RedirectToAction("AddFanfic", "Fanfic");
        }
    }
}