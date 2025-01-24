using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using UmatiGateway;
using UmatiGateway.OPC;
using UmatiGateway.OPC.CustomEncoding;

namespace umatiGateway.Pages
{
    public class CustomEncodingsModel : PageModel
    {
        private String SessionId = "";
        private ClientFactory ClientFactory;
        public CustomEncodingManager CustomEncodingsManager { get; set; } = new CustomEncodingManager();
        public CustomEncodingsModel(ClientFactory clientFactory)
        {
            this.ClientFactory = clientFactory;
        }
        public void OnGet()
        {
            UmatiGatewayApp client = this.getClient();
            this.CustomEncodingsManager = client.MqttProvider.customEncodingManager;
        }
        public IActionResult OnPostSave(bool? GMSResultDataTypeEncoding, bool? ProcessingCategoryDataTypeEncoding)
        {
            UmatiGatewayApp client = this.getClient();
            Configuration config = client.configuration;
            CustomEncodingManager customEncodingManager = client.MqttProvider.customEncodingManager;
            ManagedCustomEncoding? gms = customEncodingManager.GetManagedCustomEncodingByName("GMSResultDataTypeEncoding");
            ManagedCustomEncoding? pc = customEncodingManager.GetManagedCustomEncodingByName("ProcessingCategoryDataTypeEncoding");
            if (gms != null)
            {
                if (GMSResultDataTypeEncoding == null)
                {
                    gms.IsActive = false;
                }
                else
                {
                    gms.IsActive = true;
                }
            }
            if (pc != null)
            {
                if (ProcessingCategoryDataTypeEncoding == null)
                {
                    pc.IsActive = false;
                }
                else
                {
                    pc.IsActive = true;
                }
            }
            customEncodingManager.SaveConfiguration(config.configFilePath);
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
