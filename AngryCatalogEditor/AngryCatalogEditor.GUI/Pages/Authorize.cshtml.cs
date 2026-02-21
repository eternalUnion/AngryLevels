using AngryCatalogEditor.GUI.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace AngryCatalogEditor.GUI.Pages
{
	[IgnoreAntiforgeryToken]
	public class AuthorizeModel : PageModel
    {
		// Internal classes

		// https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#overview-of-the-device-flow
		class DeviceCodeResp
		{
			/// <summary>
			/// The device verification code is 40 characters and used to verify the device.
			/// </summary>
			public string device_code { get; set; }

			/// <summary>
			/// The user verification code is displayed on the device so the user can enter the code in a browser. This code is 8 characters with a hyphen in the middle.
			/// </summary>
			public string user_code { get; set; }

			/// <summary>
			/// The verification URL where users need to enter the user_code: https://github.com/login/device.
			/// </summary>
			public string verification_uri { get; set; }

			/// <summary>
			/// The number of seconds before the device_code and user_code expire. The default is 900 seconds or 15 minutes.
			/// </summary>
			public int expires_in { get; set; }

			/// <summary>
			/// The minimum number of seconds that must pass before you can make a new access token request (POST https://github.com/login/oauth/access_token) to complete the device authorization. For example, if the interval is 5, then you cannot make a new request until 5 seconds pass. If you make more than one request over 5 seconds, then you will hit the rate limit and receive a slow_down error.
			/// </summary>
			public int interval { get; set; }
		}

		class PostCodeResp
		{
			public string user_code { get; set; }
			public string verification_uri { get; set; }
			public int expires_in { get; set; }
		}

		class AccessTokenResp
		{
			public string access_token { get; set; }
			public string token_type { get; set; }
			public string scope { get; set; }

			public string? error { get; set; }
		}

		class GetTokenResp
		{
			public bool tokenReceived { get; set; }
			public string? error { get; set; }
		}

		// Fields

		private static DeviceCodeResp? lastRequest;
		private static DateTime lastRequestTime = DateTime.Now;
		private static DateTime lastPollTime = DateTime.Now;

		public static string? Token { get; private set; }

		// Methods

		public async Task<IActionResult> OnPostCode()
		{
			if (lastRequest != null && (DateTime.Now - lastRequestTime).TotalSeconds < lastRequest.expires_in)
			{
				return Content(JsonConvert.SerializeObject(new PostCodeResp()
				{
					user_code = lastRequest.user_code,
					verification_uri = lastRequest.verification_uri,
					expires_in = lastRequest.expires_in - (int)(DateTime.Now - lastRequestTime).TotalSeconds
				}), "application/json");
			}

			var authUrl =
				"https://github.com/login/device/code" +
				$"?client_id={AppConfig.ClientID}" +
				$"&scope=read:user%20public_repo";

			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			var resp = await client.PostAsync(authUrl, null);

			if (!resp.IsSuccessStatusCode)
			{
				return BadRequest("Failed to obtain device code from GitHub endpoint");
			}

			DeviceCodeResp? response = null;
			try
			{
				response = JsonConvert.DeserializeObject<DeviceCodeResp>(await resp.Content.ReadAsStringAsync());
			}
			catch (Exception _)
			{
				return BadRequest("Failed to parse device code response from GitHub endpoint. Response from GitHub:\n" + await resp.Content.ReadAsStringAsync());
			}

			if (response == null)
			{
				return BadRequest("Failed to parse device code response from GitHub endpoint");
			}

			lastRequest = response;
			lastRequestTime = DateTime.Now;
			lastPollTime = DateTime.Now;

			return Content(JsonConvert.SerializeObject(new PostCodeResp()
			{
				user_code = response.user_code,
				verification_uri = response.verification_uri,
				expires_in = response.expires_in
			}), "application/json");
		}
	
		public async Task<IActionResult> OnGetToken()
		{
			if (Token != null)
				return Content(JsonConvert.SerializeObject(new GetTokenResp()
				{
					tokenReceived = true
				}), "application/json");

			if (lastRequest == null)
				return BadRequest("No pending request");

			if ((DateTime.Now - lastPollTime).TotalSeconds < lastRequest.interval + 1)
				await Task.Delay((int)((lastRequest.interval * 1000 + 1000) - (DateTime.Now - lastPollTime).TotalMilliseconds));
			lastPollTime = DateTime.Now;

			HttpClient tokenClient = new HttpClient();
			tokenClient.DefaultRequestHeaders.Add("Accept", "application/json");

			var tokenResponse = await tokenClient.PostAsync("https://github.com/login/oauth/access_token", new FormUrlEncodedContent(
				new Dictionary<string, string>()
				{
					{ "client_id", AppConfig.ClientID },
					{ "device_code", lastRequest.device_code },
					{ "grant_type", "urn:ietf:params:oauth:grant-type:device_code" }
				}
			));

			if (!tokenResponse.IsSuccessStatusCode)
				return BadRequest("Access token request failed");

			AccessTokenResp? tokenResponseObj = JsonConvert.DeserializeObject<AccessTokenResp>(await tokenResponse.Content.ReadAsStringAsync());
			if (tokenResponseObj == null)
				return BadRequest("Could not parse access token response");

			if (tokenResponseObj.error != null)
			{
				switch (tokenResponseObj.error)
				{
					case "slow_down":
						lastPollTime = DateTime.Now.AddSeconds(6).AddSeconds(lastRequest.interval);
						break;

					case "unsupported_grant_type":
					case "incorrect_client_credentials":
					case "incorrect_device_code":
					case "device_flow_disabled":
						Console.WriteLine($"Bad request parameters: {tokenResponseObj.error}");
						lastRequest = null;
						break;

					case "access_denied":
					case "expired_token":
						lastRequest = null;
						break;
				}

				return Content(JsonConvert.SerializeObject(new GetTokenResp()
				{
					error = tokenResponseObj.error
				}), "application/json");
			}

			Token = tokenResponseObj.access_token;

			HttpClient userRequest = new HttpClient();
			userRequest.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
			userRequest.DefaultRequestHeaders.UserAgent.ParseAdd("AngryCatalogEditor");
			userRequest.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
			var userRequestResponse = await userRequest.GetAsync("https://api.github.com/user");

			if (userRequestResponse.IsSuccessStatusCode)
			{
				var userJson = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(await userRequestResponse.Content.ReadAsStringAsync());
				GitHandler.username = userJson.GetProperty("login").GetString();
				GitHandler.email = $"{GitHandler.username}@users.noreply.github.com";

				Response.Cookies.Append("username", GitHandler.username);
				Response.Cookies.Append("email", GitHandler.email);
			}
			else
			{
				Console.WriteLine($"Could not obtain user information, status code {userRequestResponse.StatusCode}:\n{await userRequestResponse.Content.ReadAsStringAsync()}");
			}

			return Content(JsonConvert.SerializeObject(new GetTokenResp()
				{
					tokenReceived = true
				}), "application/json");
		}
	}
}
