using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace DeepDesignLab.Base {
    public static class DeepDesignExtensions {
       public static float[] Add(this float[] a, float[] b) {
            if (a.Length == b.Length) {
                float[] output = new float[a.Length];
                for (int i = 0; i < a.Length; i++) {
                    output[i] = a[i] + b[i];
                }
                return output;
            }
            return null;
        }
        public static void Multiply(this float[] a, float b) {
            for (int i = 0; i < a.Length; i++) {
                a[i] = a[i] * b;
            }
        }

        /// <summary>
        /// Maps the value from a specified bounds to a specified bounds.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxIn"></param>
        /// <param name="minIn"></param>
        /// <param name="maxOut"></param>
        /// <param name="minOut"></param>
        /// <returns></returns>
        public static double Map(this double value, double maxIn, double minIn, double maxOut, double minOut)
        {
            var numOut = (value - minIn) / (maxIn - minIn) * (maxOut - minOut) + minOut;
            if (numOut < minOut) return minOut;
            if (numOut > maxOut) return maxOut;
            return numOut;
        }
        /// <summary>
        /// Maps the value from a specified bounds to 0 to 1.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxIn"></param>
        /// <param name="minIn"></param>
        /// <returns></returns>
        public static double Map(this double value, double maxIn, double minIn)
        {
            return value.Map(maxIn, minIn,1,0);
        }
        /// <summary>
        /// Maps the value from a specified bounds to 0 to 1. 
        /// The Scale value will power the result. Hence >1 reduces low numbers and <1 reduces high numbers.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxIn"></param>
        /// <param name="minIn"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static double Map(this double value, double maxIn, double minIn,double scale)
        {
            double outVal = value.Map(maxIn, minIn, 1, 0);
            return Math.Pow(outVal, scale);
        }

        public static bool saveData(this string destination, object data)
        {
            try
            {
                using (FileStream sw = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {

                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(sw, data);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e.Message);
                return false;
            }
            UnityEngine.Debug.Log("Saved data to " + destination);
            return true;
        }

        public static bool readData(this string destination, out object data)
        {
            data = null;
            try
            {
                using (FileStream sw = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    if (!File.Exists(destination))
                    {
                        UnityEngine.Debug.LogError("File not found");
                        return false;
                    }

                    BinaryFormatter bf = new BinaryFormatter();
                    data = bf.Deserialize(sw);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e.Message);
                return false;
            }
            UnityEngine.Debug.Log("Read data from " + destination);
            return true;
        }



        public static IEnumerable<TValue> RandomValues<TKey, TValue>(this IDictionary<TKey, TValue> dict)
        {
            System.Random rand = new System.Random();
            List<TValue> values = Enumerable.ToList(dict.Values);
            int size = dict.Count;
            while (true)
            {
                yield return values[rand.Next(size)];
            }
        }

    }
}
