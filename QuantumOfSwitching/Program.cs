using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FindTimeQuant
{
    class Program
    {
        static void Main(string[] args)
        {
            var processorNum = args.Length > 0 ? int.Parse(args[0]) - 1 : 7;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)(1 << processorNum);

            // var list = new List<string>();

            var list1 = new List<long>();
            var t = new Thread(() =>
            {
                var sw = new Stopwatch();

                for (long i = 0; i < 10000000; i++)
                {
                    sw.Stop();
                    list1.Add(sw.ElapsedMilliseconds);
                    var x = 0;

                    sw.Restart();
                }
            });
            t.Start();
            
            var sw = new Stopwatch();
            var list2 = new List<long>();
            for (long i = 0; i < 100000000; i++)
            {
                sw.Stop();
                list2.Add(sw.ElapsedMilliseconds);
                var x = 0;
                    
                sw.Restart();
            }

            t.Join();
            
            
            Console.Write(list1.Max());
            Console.Write(list2.Max());
        }
    }
}