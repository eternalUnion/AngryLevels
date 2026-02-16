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
	}
}
