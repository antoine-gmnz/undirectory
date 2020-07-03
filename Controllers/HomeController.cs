using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Graph=Microsoft.Graph;
using Microsoft.Identity.Web;
using WebApp_OpenIDConnect_DotNet.Infrastructure;
using WebApp_OpenIDConnect_DotNet.Models;
using WebApp_OpenIDConnect_DotNet.Services;
using System.Net.Http;
using System.Linq;
using System.Security.Cryptography;

namespace WebApp_OpenIDConnect_DotNet.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        readonly ITokenAcquisition tokenAcquisition;
        readonly WebOptions webOptions;

        public HomeController(ITokenAcquisition tokenAcquisition,
                              IOptions<WebOptions> webOptionValue)
        {
            this.tokenAcquisition = tokenAcquisition;
            this.webOptions = webOptionValue.Value;
        }

        public IActionResult Index()
        {
            return View();
        }

        [AuthorizeForScopes(Scopes = new[] { Constants.ScopeUserRead })]
        public async Task<IActionResult> Profile()
        {
            // Initialize the GraphServiceClient. 
            Graph::GraphServiceClient graphClient = GetGraphServiceClient(new[] { Constants.ScopeUserRead });

            var me = await graphClient.Me.Request().GetAsync();
            ViewData["Me"] = me;

            try
            {
                // Get user photo
                using (var photoStream = await graphClient.Me.Photo.Content.Request().GetAsync())
                {
                    byte[] photoByte = ((MemoryStream)photoStream).ToArray();
                    ViewData["Photo"] = Convert.ToBase64String(photoByte);
                }
            }
            catch (System.Exception)
            {
                ViewData["Photo"] = null;
            }

            return View();
        }

        private Graph::GraphServiceClient GetGraphServiceClient(string[] scopes)
        {
            return GraphServiceClientFactory.GetAuthenticatedGraphClient(async () =>
            {
                string result = await tokenAcquisition.GetAccessTokenForUserAsync(scopes);
                return result;
            }, webOptions.GraphApiUrl);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [Route("/Check")]
        public async Task<ViewResult> Check()
        {
            var password = HttpContext.Request.Form["pass"];
            //Get 5 first letter from sha1 password
            var hashUtils = new HashUtils();
            var hash = hashUtils.GetHashFromPassword(password);

            //Init HTTP Client
            HttpClient client = new HttpClient();
            //Set Header with HaveIBeenPwned Api key
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("hibp-api-key", "1bd4792f56e44a558be69e44e964ad8a");
            using (var response = await client.GetAsync("https://api.pwnedpasswords.com/range/"+hash.Substring(0, 5)))
            {
                if (!response.IsSuccessStatusCode)
                    Index();

                var responseContent = await response.Content.ReadAsStringAsync();
                var results = responseContent.Split('\n').ToList();
                foreach (string res in results){
                    if (hash.ToUpper() == hash.ToUpper()+res.Split(":")[0])
                    {
                        Console.WriteLine("FOUUUUUUNDDDDDDDD");
                    }
                    else
                    {
                        Console.WriteLine(hash.ToUpper() + " | " + res.Split(":")[0]);
                    };
                }
                Console.WriteLine(responseContent);
                //var deserializedResponse = JsonConvert.DeserializeObject<List<string>>(responseContent);

                return View("/Views/Home/index.cshtml");
            }
        }
    }
}