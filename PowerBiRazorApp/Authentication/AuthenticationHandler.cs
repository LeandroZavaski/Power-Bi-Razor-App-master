using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Newtonsoft.Json;
using PowerBiRazorApp.Models;

namespace PowerBiRazorApp.Authentication.AuthenticationHandler
{
    public class AuthenticationHandler
    {

        private readonly AzureAdSettings _azureSettings;
        private readonly PowerBiSettings _powerBiSettings;

        public AuthenticationHandler(IOptions<AzureAdSettings> azureOptions, IOptions<PowerBiSettings> powerBiOptions)
        {
            _azureSettings = azureOptions.Value;
            _powerBiSettings = powerBiOptions.Value;
        }

        /// <returns></returns>
        public async Task<(TokenCredentials tokenCredentials, string accessToken)> GetAzureTokenDataAsync()
        {

             var authorityUrl = $"{_azureSettings.Instance}{_azureSettings.TenantId}/oauth2/token";

             var oauthEndpoint = new Uri(authorityUrl);

             using (var client = new HttpClient())
             {
                 var result = await client.PostAsync(oauthEndpoint, new FormUrlEncodedContent(new[]
                 {
                     new KeyValuePair<string, string>("resource", _powerBiSettings.ResourceAddress),
                     new KeyValuePair<string, string>("client_id", _azureSettings.ClientId),
                     new KeyValuePair<string, string>("grant_type", "password"),
                     new KeyValuePair<string, string>("username", _powerBiSettings.MasterUser),
                     new KeyValuePair<string, string>("password", _powerBiSettings.MasterKey),
                     new KeyValuePair<string, string>("scope", "openid"),
                 }));

                 var content = await result.Content.ReadAsStringAsync();

                 var authenticationResult = JsonConvert.DeserializeObject<OAuthResult>(content);
                 return (new TokenCredentials(authenticationResult.AccessToken, "Bearer"), authenticationResult.AccessToken);
             }
            
        }
    }
}