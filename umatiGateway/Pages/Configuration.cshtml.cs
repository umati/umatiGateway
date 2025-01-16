using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using UmatiGateway.OPC;

namespace UmatiGateway.Pages
{
    public class ConfigurationModel : PageModel
    {
        private String SessionId = "";
        private ClientFactory ClientFactory;
        public Configuration configuration { get; set; } = new Configuration();
        public Configuration loadedConfiguration { get; set; } = new Configuration();

        public ConfigurationModel(ClientFactory ClientFactory)
        {
            this.ClientFactory = ClientFactory;
        }
        public void OnGet()
        {
            UmatiGatewayApp client = this.getClient();
            this.configuration = client.configuration;
            this.loadedConfiguration = client.loadedConfiguration;
        }
        public IActionResult OnPostDownload()
        {
            // Create file content (this could be any dynamically generated content)
            var fileContent = "Hello, this is a sample text file generated in Razor Pages!";

            // Convert the content to a byte array
            var byteArray = Encoding.UTF8.GetBytes(fileContent);

            // Return the file with content, MIME type, and suggested file name
            return File(byteArray, "text/plain", "sample.txt");
        }
        public IActionResult OnPostLoad(string FileContent)
        {
            UmatiGatewayApp client = this.getClient();
            ConfigurationReader configReader = new ConfigurationReader();
            this.loadedConfiguration = configReader.ReadConfiguration(FileContent);
            client.loadedConfiguration = this.loadedConfiguration;
            return RedirectToPage();
        }
        private UmatiGatewayApp getClient()
        {
            string? mySessionId = HttpContext.Session.GetString("SessionId");
            if (mySessionId == null)
            {
                this.SessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("SessionId", this.SessionId);
            }
            else
            {
                this.SessionId = mySessionId;
            }
            UmatiGatewayApp client = ClientFactory.getClient(this.SessionId);
            return client;
        }
    }
}
