using System;

namespace Veeam.Signature
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Usage: Signature.exe [source file] [block size]");
                    return;
                }

                if (!int.TryParse(args[1], out int blockSize) || blockSize <= 0)
                    throw new Exception("Block size should be a positive number");

                HashProcessor.Run(args[0], blockSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
