using Microsoft.AspNetCore.Mvc;

namespace OpenAPI.API.Controllers
{
    public class BaseController : Controller
    {
        protected readonly IConfiguration _configuration;

        public BaseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
    }
}
