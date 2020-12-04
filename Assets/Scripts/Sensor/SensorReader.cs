using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepDesignLab.Base;
using DeepDesignLab.Debug;
using Unity.Collections;
using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace DeepDesignLab.Sensors {
    public enum ReadingType { Temp, Humid, Pressure,Lux, IR, Visible, UV, Total_Light, eCO2, VOCs, Min, Sec,Scaled_IR};

    [System.Serializable]
    public class Reading
    {
        static double[] maxHours = new double[] { 1, 3, 6, 12, 24, 24 * 7, 24 * 30, 24 * 365 };
        static double[] perHourMaxHours = new double[] {24 * 7, 24 * 7 * 30, 24 * 7 * 365 };
        public double value { get; private set; }
        public double[] averages;//averagedHour, averaged8thDay, averaged4thDay, averagedHalfDay, averagedDay, averagedWeek, averagedMonth, averagedYear;
        public double[] averagesPerHour;

        int[] Iaverages;


        public DateTime date { get; private set; }

        /// <summary>
        /// Creates a reading with averages. 
        /// Assumes that this reading has been added to the allDates and allValues lists!
        /// </summary>
        /// <param name="_value"></param>
        /// <param name="_date"></param>
        /// <param name="pastReading"></param>
        /// <param name="AllDates"></param>
        /// <param name="AllValues"></param>
        /// <param name="hoursIndex"></param>
        public Reading(double _value, DateTime _date, Reading pastReading, List<DateTime> AllDates, List<double> AllValues, List<int> hoursIndex)
        {
            value = _value;
            date = _date;

            averages = new double[maxHours.Length];
            Iaverages = new int[maxHours.Length];
            averagesPerHour = new double[perHourMaxHours.Length];

            if (pastReading==null)
            {
                for (int i = 0; i < maxHours.Length; i++)
                {
                    Iaverages[i] = 0;
                    averages[i] = _value;
                }
            }
            else
            {
                for (int i = 0; i < maxHours.Length; i++)
                {
                    while ((_date - AllDates[Iaverages[i]]).TotalHours > maxHours[i])
                    {
                        Iaverages[i]++;
                    }
                    averages[i] = AllValues.GetRange(Iaverages[i], AllValues.Count - Iaverages[i]).Average();
                }

            }

            for (int i = 0; i < perHourMaxHours.Length; i++)
            {
                List<double> tempVals = new List<double>();
                for (int j = 0; j < hoursIndex.Count; j++)
                {
                    if ((_date - AllDates[hoursIndex[j]]).TotalHours < perHourMaxHours[i])
                    {
                        tempVals.Add(AllValues[j]);
                    }
                }
                averagesPerHour[i] = tempVals.Count>0 ? tempVals.Average(): _value;
            }
        }
    }


    public class SensorReader
    {

        Stopwatch timer = new Stopwatch();
        int nLinesPerLoop = 1000;
        int msPerLoop = 20;

        string settingsFileName = "sensorReaderData.dat";
        string settingsFilePath;
        bool useSettingsFile = false;
        bool overWriteSettingsFile = false;

        Dictionary<ReadingType, string> HeaderName = new Dictionary<ReadingType, string>();

        CSVreader FileReader = new CSVreader(1);
        string filePath = null;//Debug
        string directory = null;
        bool haveReadDatFile = false;


        string[] HeaderValues;//Debug
        int nDataPoins;//Debug

        // int entry = 9;//Debug
        // [ReadOnly] public double[] values;

        //string[] rows;
        List<double[]> NumberData = new List<double[]>();
        int nDatapointErrors;//Debug

        bool hasLoadedValues = false;//Debug
        bool hasReadFile = false;//Debug
        public int percentDone = 0;
        //bool startedJob = false;

        public bool isDone { get { return hasLoadedValues; } }


        public Dictionary<ReadingType, List<double>> Data = new Dictionary<ReadingType, List<double>>();
        public List<DateTime> Dates = new List<DateTime>();

        public Dictionary<ReadingType, List<double>> AverageValues = new Dictionary<ReadingType, List<double>>();

        public Dictionary<ReadingType, List<Reading>> Readings = new Dictionary<ReadingType, List<Reading>>();
       

        Dictionary<ReadingType, List<int>[]> AverageIndex = new Dictionary<ReadingType, List<int>[]>();

        public List<DateTime> AverageDates = new List<DateTime>();

        int Counter = 0;


        public SensorReader(string path, bool readFromData, bool overwriteSettings)
        {
            filePath = path;
            directory = Path.GetDirectoryName(filePath);
            settingsFileName = Path.GetFileNameWithoutExtension(filePath) + settingsFileName;
            settingsFilePath = Path.Combine(directory, "_" + settingsFileName);
            useSettingsFile = readFromData;
            overWriteSettingsFile = overwriteSettings;
            Start();
        }

        // Use this for initialization
        void Start()
        {
            FileReader.readFile(filePath);
            hasReadFile = false;
            //startedJob = true;
            hasLoadedValues = false;

            HeaderName.Add(ReadingType.Lux, "TSL_Lux");             // 2147483.75 is false recording
            HeaderName.Add(ReadingType.IR, "TSL_IR");               // 1 can be false or true recording
            HeaderName.Add(ReadingType.Visible, "TSL_Vis");         // -1 is false recording <------------------------this to validate TSL sensor
            HeaderName.Add(ReadingType.Total_Light, "TSL_Full");    // 0 can be false or true recording
            HeaderName.Add(ReadingType.Temp, "BME_Temp");
            HeaderName.Add(ReadingType.Humid, "BME_Hum");
            HeaderName.Add(ReadingType.Pressure, "BME_Pres");
            HeaderName.Add(ReadingType.UV, "VEML_UV");              // 65535 is NaN, false recording<-----------------this to validate VEML sensor
            HeaderName.Add(ReadingType.eCO2, "SGP_CO2");            // 400 is uncalibrated<-----------/---------------Both to validate SGP sensor
            HeaderName.Add(ReadingType.VOCs, "SGP_VOCs");           // 0 is uncalibrated<------------/
            HeaderName.Add(ReadingType.Min, "Minute");
            HeaderName.Add(ReadingType.Sec, "Second");


            if (useSettingsFile && File.Exists(settingsFilePath))
            {
                object outObj;
                haveReadDatFile= settingsFilePath.readData(out outObj);
                if (haveReadDatFile) Readings = (Dictionary<ReadingType, List<Reading>>)outObj;
            }

            for (int i = 0; i < Enum.GetNames(typeof(ReadingType)).Length; i++)
            {
                AverageValues.Add((ReadingType)i, new List<double>());
                if(!haveReadDatFile) Readings.Add((ReadingType)i, new List<Reading>());
                Data.Add((ReadingType)i, new List<double>());
                AverageIndex.Add((ReadingType)i, new List<int>[24]);
            }


            // Set up graph properties using our graph keys
            // DebugGUI.SetGraphProperties("Lux", "Lux", 0, 2700, -1, new Color(0, 1, 1), false);

        }

        // Update is called once per frame
        public IEnumerator Update()
        {
            while (!hasLoadedValues)
            {
                if (!hasLoadedValues && FileReader.hasFinished)
                {
                    hasReadFile = true;
                    HeaderValues = FileReader.getHeader;
                    NumberData = FileReader.CopyData;
                    nDataPoins = FileReader.getnDataPoints;

                    double lastMin = -1;

                    int TempSecond = -1;
                    DateTime timestamp;

                    // Instantiate lists within dictionaries
                    Dictionary<ReadingType, List<double>> tempValues = new Dictionary<ReadingType, List<double>>();
                    for (int i = 0; i < Enum.GetNames(typeof(ReadingType)).Length; i++)
                    {
                        tempValues.Add((ReadingType)i, new List<double>());
                        for (int j = 0; j < AverageIndex[(ReadingType)i].Length; j++)
                        {
                            AverageIndex[(ReadingType)i][j] = new List<int>();
                        }
                    }
                    double tempAverageValue = -1;


                    lastMin = NumberData[0][Array.IndexOf(HeaderValues, "Minute")];
                    TempSecond = 0;
                    timer.Start();
                    nLinesPerLoop = 0;


                    for (int i = 0; i < nDataPoins; i++)
                    {
                        if (timer.ElapsedMilliseconds > msPerLoop)
                        {
                            //UnityEngine.Debug.Log(string.Format("Reading {3}, at {0:P}%. Reading {1} per loop at {2}ms.", i * 1.0 / nDataPoins, i - nLinesPerLoop, timer.ElapsedMilliseconds, i));
                            nLinesPerLoop = i;
                            percentDone = (int)(i * 100.0 / nDataPoins);
                            yield return null;
                            timer.Stop();
                            timer.Reset();
                            timer.Start();
                        }

                        if (NumberData[i].Length != HeaderValues.Length)
                        {
                            nDatapointErrors++;
                            UnityEngine.Debug.Log(string.Format("Error at row {0}", i));
                        }
                        else
                        {
                            /////////////////////////////////////
                            // 1) Check if the new value is within the averaging minute. If not find the average and reset temp variables.

                            // This section averages the values per minute.
                            if (NumberData[i][Array.IndexOf(HeaderValues, "Minute")] != lastMin)
                            {
                                lastMin = NumberData[i][Array.IndexOf(HeaderValues, "Minute")];
                                TempSecond = 0;
                                // Use the last values [i-1] for the average date.
                                timestamp = new DateTime((int)NumberData[i - 1][Array.IndexOf(HeaderValues, "Year")],
                                                        (int)NumberData[i - 1][Array.IndexOf(HeaderValues, "Month")],
                                                        (int)NumberData[i - 1][Array.IndexOf(HeaderValues, "Day")],
                                                        (int)NumberData[i - 1][Array.IndexOf(HeaderValues, "Hour")],
                                                        (int)NumberData[i - 1][Array.IndexOf(HeaderValues, "Minute")],
                                                        TempSecond);
                                if (timestamp.Year == 1999)
                                {
                                    timestamp = AverageDates[AverageDates.Count - 1];
                                    timestamp.AddMinutes((AverageDates[AverageDates.Count - 1] - AverageDates[AverageDates.Count - 2]).Minutes);
                                    timestamp.AddSeconds((AverageDates[AverageDates.Count - 1] - AverageDates[AverageDates.Count - 2]).Seconds);
                                    timestamp.AddHours((AverageDates[AverageDates.Count - 1] - AverageDates[AverageDates.Count - 2]).Hours);
                                    timestamp.AddDays((AverageDates[AverageDates.Count - 1] - AverageDates[AverageDates.Count - 2]).Days);
                                }
                                AverageDates.Add(timestamp);
                                // Average the temp list, add to the global average list.
                                // Reset temp enums
                                for (int j = 0; j < Enum.GetNames(typeof(ReadingType)).Length; j++)
                                {
                                    if ((ReadingType)j != ReadingType.Scaled_IR)
                                    {
                                        tempAverageValue = tempValues[(ReadingType)j].Average();
                                        AverageValues[(ReadingType)j].Add(tempAverageValue);
                                        AverageIndex[(ReadingType)j][timestamp.Hour].Add(AverageDates.Count - 1);
                                        if (!haveReadDatFile) Readings[(ReadingType)j].Add(new Reading(tempAverageValue,
                                                                                    timestamp,
                                                                                    Readings[(ReadingType)j].Count > 0 ? Readings[(ReadingType)j][Readings[(ReadingType)j].Count - 1] : null,
                                                                                    AverageDates,
                                                                                    AverageValues[(ReadingType)j],
                                                                                    AverageIndex[(ReadingType)j][timestamp.Hour]));

                                        // update averageing windows.


                                        tempValues[(ReadingType)j].Clear();
                                    }

                                }
                                if (!haveReadDatFile)
                                {
                                    double scaledIR = AverageValues[ReadingType.Total_Light].Last() == 0 ? 0 : (AverageValues[ReadingType.IR].Last()<10?0: AverageValues[ReadingType.IR].Last()) / AverageValues[ReadingType.Total_Light].Last();
                                    AverageValues[ReadingType.Scaled_IR].Add(scaledIR);
                                    AverageIndex[ReadingType.Scaled_IR][timestamp.Hour].Add(AverageDates.Count - 1);
                                    Readings[ReadingType.Scaled_IR].Add(new Reading(scaledIR,
                                                                                timestamp,
                                                                                Readings[ReadingType.Scaled_IR].Count > 0 ? Readings[ReadingType.Scaled_IR][Readings[ReadingType.Scaled_IR].Count - 1] : null,
                                                                                AverageDates,
                                                                                AverageValues[ReadingType.Scaled_IR],
                                                                                AverageIndex[ReadingType.Scaled_IR][timestamp.Hour]));
                                }
                            }
                            /////////////////////////////////////
                            // 2) Calculate the time where the seconds incriment by 1 for each burst value.
                            TempSecond++;
                            timestamp = new DateTime((int)NumberData[i][Array.IndexOf(HeaderValues, "Year")],
                                                        (int)NumberData[i][Array.IndexOf(HeaderValues, "Month")],
                                                        (int)NumberData[i][Array.IndexOf(HeaderValues, "Day")],
                                                        (int)NumberData[i][Array.IndexOf(HeaderValues, "Hour")],
                                                        (int)NumberData[i][Array.IndexOf(HeaderValues, "Minute")],
                                                        TempSecond);
                            Dates.Add(timestamp);

                            /////////////////////////////////////
                            // 3) Add the current value to the average list, global list and date to the global date list.
                            for (int j = 0; j < Enum.GetNames(typeof(ReadingType)).Length; j++)
                            {
                                // Check if the Enum is within the data.
                                if ((ReadingType)j != ReadingType.Scaled_IR && Array.IndexOf(HeaderValues, HeaderName[(ReadingType)j]) >= 0)
                                {
                                    // It is in data, now add to Total list and temp list for 

                                    tempValues[(ReadingType)j].Add(NumberData[i][Array.IndexOf(HeaderValues, HeaderName[(ReadingType)j])]);
                                    Data[(ReadingType)j].Add(NumberData[i][Array.IndexOf(HeaderValues, HeaderName[(ReadingType)j])]);
                                }
                            }
                        }

                    }
                    UnityEngine.Debug.Log(string.Format("All done setting up of readings..."));
                    timer.Restart();
                    if (overWriteSettingsFile || (useSettingsFile&& !File.Exists(settingsFilePath)))
                    {
                        settingsFilePath.saveData(Readings);
                        UnityEngine.Debug.Log(string.Format("Saved data to dat file in {0}s", timer.ElapsedMilliseconds * 1.0 / 1000));
                    }
                    hasLoadedValues = true;
                }
                yield return null;
            }

        }


    }
}