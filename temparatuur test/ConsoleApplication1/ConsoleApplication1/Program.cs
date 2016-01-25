using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            lijst(100);
        }


        public List<string> lijst(int k)
        {
            int a = 1;
            int b = 2;
            List<string> list1 = new List<string>();


            list1.Add(Convert.ToString(0));



            while (k > a * b)
            {
                list1.Add(Convert.ToString(a * b));

            }

            return (list1);
        }
    }
}
