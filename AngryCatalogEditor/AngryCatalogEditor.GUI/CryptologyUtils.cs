using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AngryCatalogEditor.GUI
{
	public static class CryptologyUtils
	{
		public static string GetMD5Hash(string data)
		{
			MD5 md5 = MD5.Create();
			byte[] hashArr = md5.ComputeHash(Encoding.ASCII.GetBytes(data));
			return Convert.ToHexString(hashArr).ToLower();
		}

		public static string GetMD5Hash(byte[] data)
		{
			MD5 md5 = MD5.Create();
			byte[] hashArr = md5.ComputeHash(data);
			return Convert.ToHexString(hashArr).ToLower();
		}

		public static string GetMD5Hash(Stream stream)
		{
			MD5 md5 = MD5.Create();
			byte[] hashArr = md5.ComputeHash(stream);
			return Convert.ToHexString(hashArr).ToLower();
		}

		private static readonly char[] certificatePublicKey = """
			-----BEGIN PUBLIC KEY-----
			MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA+/ueeOpso05dA+5GjKbj
			Q0VpM+JAHmRRgYRw36G4dXqmpCGfVDNVdjjBBkVWO+6lJoSNaaG4Yprn4uQVslUQ
			7OYWAw6Y+9E0Ezvr1quWE7i0KGxG6weplRTsu9aO0/9gJgP/gWQxC0Cf83NwyvMP
			sThtCruAQFT+cW0LGghtFgrBr++aknI06SJI5ydrbZgEtU5i4FfjrV1ms4CRRojh
			ydJglfGQfG8W3pTDge4jVdND+RGB6F01QGi0+Bnq5DfKdjvb3/Zh1ko7WocWgavD
			aIgLYj88AgbGdC0lidLMIgzdnGxkLyxbTzsgi/mvUpB2foy4uHoV22EaWMj+6H+o
			XQIDAQAB
			-----END PUBLIC KEY-----
			
			""".ToCharArray();

		public static bool VerifyCertificate(string path, string certificatePath)
		{
			string currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			RSA key = RSA.Create();
			key.ImportFromPem(certificatePublicKey);
			byte[] data = File.ReadAllBytes(path);
			byte[] sig = File.ReadAllBytes(certificatePath);

			return key.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		}
	}
}
