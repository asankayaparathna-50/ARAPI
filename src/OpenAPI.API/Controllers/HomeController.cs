using Microsoft.AspNetCore.Mvc;

namespace OpenAPI.API.Controllers
{
    public class HomeController : Controller
    {

        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            //var baseUrl = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            //    .AddJsonFile("appsettings.json")
            //    .Build()
            //    .GetValue<string>("Key:BaseUrl");

                var BaseUrl = _configuration.GetValue<string>("Key:BaseUrl");
            ViewBag.baseUrl = BaseUrl;
            return View();
        }
    }
}
