namespace AngryParter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Write("File path: ");
            string path = Console.ReadLine();

            if (path.StartsWith('"'))
                path = path.Substring(1, path.Length - 2);

            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                Console.ReadKey();
                return;
            }

            string fname = Path.GetFileNameWithoutExtension(path);
            string fdir = Path.GetDirectoryName(path);

            int partNum = 1;
            int partSize = 1024 * 1024 * 24;
            using (BinaryReader stream = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read)))
            {
                Stream baseStr = stream.BaseStream;
                long remaining = baseStr.Length;

                byte[] buff = new byte[81920];
                int blockSize = 81920;

                while (remaining > 0)
                {
                    int toRead = (remaining > partSize) ? partSize : (int)remaining;
                    remaining -= toRead;

                    using (FileStream bw = File.Create(Path.Combine(fdir, fname + $".angry{partNum++}")))
                    {
                        while (toRead > 0)
                        {
                            int blockToRead = (toRead > blockSize) ? blockSize : toRead;
                            toRead -= blockToRead;

                            stream.Read(buff, 0, blockToRead);
                            bw.Write(buff, 0, blockToRead);
                        }
                    }
                }
            }
        }
    }
}