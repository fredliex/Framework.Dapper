using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Dapper.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            unsafe
            {
                int* typePorinter = (int*)typeof(AA).TypeHandle.Value.ToPointer();
                int* medthod1Porinter = (int*)typeof(AA).GetMethod(nameof(AA.nullFun1)).MethodHandle.Value.ToPointer();
                int* medthod2Porinter = (int*)typeof(AA).GetMethod(nameof(AA.nullFun2)).MethodHandle.Value.ToPointer();

                //MethodRental.SwapMethodBody
            }
        }
    }
}
