using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Veeam.Signature
{
    public class HashProcessor : IDisposable
    {
        private readonly Stream _streamIn;
        private readonly int _blockSize;

        private readonly int _threadsLimit;
        private readonly CancellationTokenSource _ctsError;
        private readonly BlockingCollection<(uint index, byte[] data)> _blocks;
        private readonly BlockingCollection<(uint index, string value)> _hashes;
        private readonly ConcurrentQueue<Exception> _exceptions;

        public static void Run(string fileIn, int blockSize)
        {
            using var hashProcessor = new HashProcessor(fileIn, blockSize);
            hashProcessor.Run();
        }

        private HashProcessor(string fileIn, int blockSize)
        {
            _streamIn = File.Open(fileIn, FileMode.Open);
            _blockSize = blockSize;

            _threadsLimit = Environment.ProcessorCount;
            _ctsError = new CancellationTokenSource();
            _blocks = new BlockingCollection<(uint, byte[])>(_threadsLimit * 2);
            _hashes = new BlockingCollection<(uint, string)>(_threadsLimit * 512);
            _exceptions = new ConcurrentQueue<Exception>();
        }

        public void Dispose()
        {
            _blocks?.Dispose();
            _hashes?.Dispose();
            _ctsError?.Dispose();
            _streamIn?.Dispose();

            GC.SuppressFinalize(this);
        }

        private void Run()
        {
            Thread readerThread = StartThread(ReadBlocks);
            Thread[] hasherThreads = Enumerable.Range(0, _threadsLimit).Select(x => StartThread(CalcHashes)).ToArray();
            Thread consoleThread = StartThread(ShowHashes);

            readerThread.Join();
            _blocks.CompleteAdding();

            foreach (Thread hasherThread in hasherThreads)
                hasherThread.Join();

            _hashes.CompleteAdding();
            consoleThread.Join();

            if (!_exceptions.IsEmpty)
                throw new AggregateException(_exceptions);
        }

        private Thread StartThread(Action action)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _exceptions.Enqueue(ex);
                    _ctsError.Cancel();
                }
            });

            thread.Start();
            return thread;
        }

        private void ReadBlocks()
        {
            using var reader = new BinaryReader(_streamIn);
            uint blockCounter = 0;

            while (_streamIn.Position < _streamIn.Length)
            {
                byte[] data = reader.ReadBytes(_blockSize);
                _blocks.Add((blockCounter++, data), _ctsError.Token);
            }
        }

        private void CalcHashes()
        {
            using var sha256 = SHA256.Create();

            foreach ((uint index, byte[] data) in _blocks.GetConsumingEnumerable(_ctsError.Token))
            {
                string hash = sha256.ComputeHash(data).ToHexString();
                _hashes.Add((index, hash), _ctsError.Token);
            }
        }

        private void ShowHashes()
        {
            foreach ((uint index, string value) in _hashes.GetConsumingEnumerable(_ctsError.Token))
                Console.WriteLine($"Block #{index}: {value}");
        }
    }
}
