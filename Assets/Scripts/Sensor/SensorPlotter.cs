using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using SciColorMaps;
using System.IO;
using DeepDesignLab.Base;
using UnityEngine.UI;

namespace DeepDesignLab.Sensors
{
    public class SensorPlotter : MonoBehaviour
    {
        public string filePath = @"C:\Users\Gladys\Documents\Repos\SensorDataVis01\Assets\Resources\Backups\SwinIndoor_Cleaned.csv";
        public string directoryPath;
        string readingsFileName = "SensorPlotterData.dat";
        string readingsDataPath;
        public int percentage = 0;
        SensorReader reader;

        ReadingType[] trackOrder = new ReadingType[] { ReadingType.Pressure, ReadingType.Humid, ReadingType.Temp,  ReadingType.Scaled_IR, ReadingType.Lux};

        public Text Display;

        float nDaysToDisplay = 7;
        float hoursPerSecondDisplayed = 6;

        MeshFilter meshy;

        ColorMap cMap = new ColorMap();

        bool isDrawingLines = false;
        bool hasDrawnLines = false;
        int tracksToUpdate = 0;

        public List<Dictionary<ReadingType, Reading>> ReadingsList;

        public  List<Dictionary<ReadingType, Reading>> renderList = new List<Dictionary<ReadingType, Reading>>();
        public  bool renderListIsDirty = false;
        int renderValuesRemoved = 0;

        List<int> allIndices = new List<int>();
        List<Color> allColours = new List<Color>();
        List<Vector3> allVerts = new List<Vector3>();

        public int indexDrawn;

        public int readingCount, renderCount;

        public DateTime currentRenderDate;
        public string date;

        DateTime drawingStart = new DateTime(2019, 5, 27, 0, 0, 0);

        // Start is called before the first frame update
        void Start()
        {
            directoryPath = Path.GetDirectoryName(filePath);
            readingsFileName = Path.GetFileNameWithoutExtension(filePath) + readingsFileName;
            readingsDataPath = Path.Combine(directoryPath, "_"+readingsFileName);
            //  renderer = GetComponent<MeshRenderer>();
            meshy = GetComponent<MeshFilter>();
            reader = new SensorReader(filePath,true,false);
            StartCoroutine(reader.Update());
            UnityEngine.Debug.Log("Finished Startup");
            //drawLine();
        }

        // Update is called once per frame
        void Update()
        {
            percentage = reader.percentDone;
            //reader.Update();
            if (!hasDrawnLines && !isDrawingLines && reader != null && reader.isDone)
            {
                if (File.Exists(readingsDataPath))
                {
                    object dataOut;
                    if (readingsDataPath.readData(out dataOut)) ReadingsList = (List<Dictionary<ReadingType, Reading>>)dataOut;
                }
                else
                {
                    ReadingsList = new List<Dictionary<ReadingType, Reading>>(reader.AverageDates.Count);
                    for (int i = 0; i < reader.AverageDates.Count; i++)
                    {
                        ReadingsList.Add(new Dictionary<ReadingType, Reading>());
                        foreach (var item in reader.Readings)
                        {
                            ReadingsList[i].Add(item.Key, item.Value[i]);
                        }
                    }
                    readingsDataPath.saveData(ReadingsList);
                }
                StartCoroutine(addLive(ReadingsList, renderList, TimeSpan.FromDays(nDaysToDisplay), hoursPerSecondDisplayed));
                UnityEngine.Debug.Log("Loaded all lines");

                isDrawingLines = true;
            }
            if (isDrawingLines)
            {
                DrawReading(renderList, TimeSpan.FromDays(nDaysToDisplay), drawingStart);
                if (Display != null) Display.text = date;
            }
            else
            {
                string points = new String('.', (int)Time.realtimeSinceStartup % 4);
                if (Display != null) Display.text = string.Format("Loading historical data{0}\t{1}% done.", points,percentage);
            }

            if (ReadingsList != null) readingCount = ReadingsList.Count;
            if (renderList != null) renderCount = renderList.Count;

        }


        /// <summary>
        /// Return a number between 0 and 1 depending on how far the toPot time is from the start time. 0 is the start time and 1 is duration TimeSpan away or more.
        /// </summary>
        /// <param name="toPlot"></param>
        /// <param name="start"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        float getThetaClamped(DateTime toPlot, DateTime start, TimeSpan duration)
        {
            TimeSpan timeFromStart = toPlot - start;
            float outTheta = (float)(timeFromStart.TotalSeconds / duration.TotalSeconds);//(float)(dates[i].Day * 1.0 / 31 + dates[i].Hour * 1.0 / 24 + dates[i].Minute * 1.0 / (60 * 24) + dates[i].Second * 1.0 / (60 * 60 * 24)) * 2 * Mathf.PI;
            if (outTheta > 1) outTheta = 1;
            if (outTheta < 0) outTheta = 0;
            return outTheta;
        }

        /// <summary>
        /// Return a number between 0 and 1 depending on how far the toPot time is from the start time. 
        /// 0 is the start time and 1 is duration TimeSpan away. If it is earlier or later than this boundary will return -1.
        /// </summary>
        /// <param name="toPlot"></param>
        /// <param name="start"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        float getTheta(DateTime toPlot, DateTime start, TimeSpan duration)
        {
            TimeSpan timeFromStart = toPlot - start;
            float outTheta = (float)(timeFromStart.TotalSeconds / duration.TotalSeconds);//(float)(dates[i].Day * 1.0 / 31 + dates[i].Hour * 1.0 / 24 + dates[i].Minute * 1.0 / (60 * 24) + dates[i].Second * 1.0 / (60 * 60 * 24)) * 2 * Mathf.PI;
            if (outTheta > 1) outTheta = outTheta - (int)outTheta;
            if (outTheta < 0) outTheta = 0;
            return outTheta;
        }

        IEnumerator AddLines(List<Reading> values, List<DateTime> dates, int track, List<int> allIndices, List<Color> allColours, List<Vector3> allVerts)
        {
            tracksToUpdate -= 1;
            double max = values.Max(val => val.value);
            double min = values.Min(val => val.value);

            DateTime start = dates.Min();
            DateTime finsih = dates.Max();

            float theta;

            System.Drawing.Color col;
            List<Vector3> tempVerts;
            for (int i = 0; i < values.Count; i++)
            {
                theta = getTheta(dates[i], start, finsih - start);
                col = cMap.GetColor((double)theta);

                getLineOnTrack(values[i].value, max, min, theta * 2 * Mathf.PI, track, Vector3.zero, out tempVerts);
                for (int j = 0; j < tempVerts.Count; j++)
                {
                    allIndices.Add(allVerts.Count + j);
                    allColours.Add(new Color(col.R * 1.0f / 255, col.G * 1.0f / 255, col.B * 1.0f / 255));
                }
                allVerts.AddRange(tempVerts);

                yield return null;
            }

            tracksToUpdate += 1;
            UnityEngine.Debug.Log("Added mesh line");
        }

        IEnumerator AddLines(List<double> values, List<DateTime> dates, int track, List<int> allIndices, List<Color> allColours, List<Vector3> allVerts)
        {
            tracksToUpdate -= 1;
            double max = values.Max();
            double min = values.Min();

            DateTime start = dates.Min();
            DateTime finsih = dates.Max();

            float theta;

            System.Drawing.Color col;
            List<Vector3> tempVerts;
            for (int i = 0; i < values.Count; i++)
            {
                theta = getTheta(dates[i], start, finsih - start);
                col = cMap.GetColor((double)theta);

                getLineOnTrack(values[i], max, min, theta * 2 * Mathf.PI, track, Vector3.zero, out tempVerts);
                for (int j = 0; j < tempVerts.Count; j++)
                {
                    allIndices.Add(allVerts.Count + j);
                    allColours.Add(new Color(col.R * 1.0f / 255, col.G * 1.0f / 255, col.B * 1.0f / 255));
                }
                allVerts.AddRange(tempVerts);

                yield return null;
            }

            tracksToUpdate += 1;
            UnityEngine.Debug.Log("Added mesh line");
        }


        void DrawReading(List<Dictionary<ReadingType, Reading>> values, TimeSpan duration,DateTime FirstRecording)
        {
            if (renderListIsDirty)
            {


                int nVertsPerValue = 2;
                List<int> Indices = new List<int>(trackOrder.Length * nVertsPerValue * values.Count);
                List<int> Triangles = new List<int>(trackOrder.Length * 3 * nVertsPerValue * values.Count);
                List<Color> Colours = new List<Color>(trackOrder.Length * nVertsPerValue * values.Count);
                List<Vector3> Verts = new List<Vector3>(trackOrder.Length * nVertsPerValue * values.Count);

                // Array.Copy(meshFilt.mesh.vertices, renderedValuesRemoved * trackOrder.Length * nVertsPerValue, Verts, 0, meshFilt.mesh.vertices.Length - renderedValuesRemoved * trackOrder.Length * nVertsPerValue);
                // Array.Copy(meshFilt.mesh.vertices, renderedValuesRemoved * trackOrder.Length * nVertsPerValue, Verts, 0, meshFilt.mesh.vertices.Length - renderedValuesRemoved * trackOrder.Length * nVertsPerValue);
                //  Array.Copy(meshFilt.mesh.vertices, renderedValuesRemoved * trackOrder.Length * nVertsPerValue, Verts, 0, meshFilt.mesh.vertices.Length - renderedValuesRemoved * trackOrder.Length * nVertsPerValue);


                for (int j = 0; j < trackOrder.Length; j++)
                {

                    double max = values.Max(val => val[trackOrder[j]].value);
                    double min = values.Min(val => val[trackOrder[j]].value);

                    int selectedAverage = 1;

                    double aveMax = values.Max(val => val[trackOrder[j]].value -val[trackOrder[j]].averagesPerHour[selectedAverage]);
                    double aveMin = values.Min(val => val[trackOrder[j]].value - val[trackOrder[j]].averagesPerHour[selectedAverage]);


                   // DateTime start = values.First()[trackOrder[j]].date;
                   // DateTime finsih = start + duration;//values.Last()[trackOrder[j]].date;

                    float theta;
                    System.Drawing.Color col;
                    List<Vector3> tempVerts;

                    for (int i = 0; i < values.Count; i++)
                    {

                        theta = getTheta(values[i][trackOrder[j]].date, FirstRecording, duration);

                        // col = cMap.GetColor((double)theta);


                        col = cMap.GetColor((values[i][trackOrder[j]].value - values[i][trackOrder[j]].averagesPerHour[selectedAverage] ).Map(aveMax, aveMin,5));

                        getLineOnTrack(values[i][trackOrder[j]].value, max, min, theta * 2 * Mathf.PI, j, Vector3.zero, out tempVerts);

                        for (int k = 0; k < tempVerts.Count; k++)
                        {
                            Indices.Add(Verts.Count + k);
                            //Indices.Add(k * values.Count * nVertsPerValue + (tempVerts.Count * i + k));
                            Colours.Add(new Color(col.R * 1.0f / 255, col.G * 1.0f / 255, col.B * 1.0f / 255));
                        }
                        if (i > 0)
                        {
                            Triangles.Add(Verts.Count - 2);
                            Triangles.Add(Verts.Count - 1);
                            for (int k = 1; k < tempVerts.Count; k++)
                            {
                                Triangles.Add(Verts.Count + k);
                            }
                            Triangles.Add(Verts.Count - 2);
                            for (int k = 0; k < tempVerts.Count; k++)
                            {
                                Triangles.Add(Verts.Count + k);
                            }
                        }

                        Verts.AddRange(tempVerts);
                    }
                }

                drawAllTriangles(Triangles, Colours, Verts);
               // drawAllLines(Indices, Colours, Verts);
                renderListIsDirty = false;

            }

        }
    

        IEnumerator addLive(List<Dictionary<ReadingType, Reading>> values, List<Dictionary<ReadingType, Reading>> RenderedValues, TimeSpan wheelLength, float drawSpeed_hrPsec)
        {
            UnityEngine.Debug.Log("adding live values");
            float startTime = Time.realtimeSinceStartup;
            int iFirst = 0;
            for (int i = 0; i < values.Count; i++)
            {
                indexDrawn = i;
                currentRenderDate = values[i].First().Value.date;
                date = currentRenderDate.ToString("dddd, hh:mm tt");
                if ((values[i].First().Value.date - values[iFirst].First().Value.date).TotalHours > ((Time.time - startTime) * drawSpeed_hrPsec))
                {
                   // UnityEngine.Debug.Log("Will Yield!");
                    yield return new WaitForSecondsRealtime((float)(values[i].First().Value.date - values[iFirst].First().Value.date).TotalHours / drawSpeed_hrPsec - ((Time.realtimeSinceStartup - startTime)));
                }
                RenderedValues.Add(values[i]);
                //UnityEngine.Debug.Log("Added values to render");
                while ((RenderedValues.Last().First().Value.date-RenderedValues.First().First().Value.date)> wheelLength)
                {
                    RenderedValues.RemoveAt(0);
                    renderValuesRemoved++;
                }
                renderListIsDirty = true;
            }
           // UnityEngine.Debug.Log("Time done");
        }

        

        void AddAllLines(List<Reading> values, List<DateTime> dates, int track, List<int> allIndices, List<Color> allColours, List<Vector3> allVerts)
        {
            tracksToUpdate -= 1;
            double max = values.Max(val => val.value);
            double min = values.Min(val => val.value);

            int selectedAverage = 0;

            double aveMax = values.Max(val => val.averagesPerHour[selectedAverage] - val.value);
            double aveMin = values.Min(val => val.averagesPerHour[selectedAverage] - val.value);


            DateTime start = dates.Min();
            DateTime finsih = dates.Max();

            float theta;

            int nVertsPerValue = 2;

            List<int> indices = new List<int>(values.Count * nVertsPerValue);
            List<Color> colours = new List<Color>(values.Count * nVertsPerValue);
            List<Vector3> verts = new List<Vector3>(values.Count * nVertsPerValue);

            System.Drawing.Color col = cMap.GetColor(0.5);
            List<Vector3> tempVerts;
            for (int i = 0; i < values.Count; i++)
            {
                theta = getTheta(dates[i], start, finsih - start);

               // col = cMap.GetColor((double)theta);


                col = cMap.GetColor((values[i].averagesPerHour[selectedAverage] - values[i].value).Map(aveMax, aveMin));

                getLineOnTrack(values[i].value, max, min, theta * 2 * Mathf.PI, track, Vector3.zero, out tempVerts);
                verts.AddRange(tempVerts);
                for (int j = 0; j < tempVerts.Count; j++)
                {
                    indices.Add(track* values.Count* nVertsPerValue + (tempVerts.Count * i+j));
                    colours.Add(new Color(col.R * 1.0f / 255, col.G * 1.0f / 255, col.B * 1.0f / 255, 0.5f));
                }

            }

            allColours.AddRange(colours.ToList());
            allIndices.AddRange(indices.ToList());
            allVerts.AddRange(verts.ToList());
            

            tracksToUpdate += 1;
            UnityEngine.Debug.Log("Added mesh line" );
        }

        void drawAllLines(List<int> allIndices, List<Color> allColours, List<Vector3> allVerts)
        {
            Mesh mesh = new Mesh();
            mesh.subMeshCount = 1;
            //mesh.vertices = verts;

            // UnityEngine.Debug.Log("vertCount is " + allVerts.Count.ToString());
            mesh.SetVertices(allVerts.ToArray());
            mesh.SetColors(allColours.ToArray());
            mesh.SetIndices(allIndices.ToArray(), MeshTopology.Lines, 0);

            mesh.RecalculateBounds();
            if (meshy != null)
            {
                meshy.mesh = mesh;
            }

            UnityEngine.Debug.Log("Drawn mesh line");
            hasDrawnLines = true;
        }

        void drawAllTriangles(List<int> allTriangles, List<Color> allColours, List<Vector3> allVerts)
        {
            Mesh mesh = new Mesh();
            mesh.subMeshCount = 1;
            //mesh.vertices = verts;

            // UnityEngine.Debug.Log("vertCount is " + allVerts.Count.ToString());
            mesh.SetVertices(allVerts.ToArray());
            mesh.SetColors(allColours.ToArray());
            mesh.SetUVs(0, allVerts.Select(x => new Vector2(x.x, x.y)).ToArray());
            mesh.SetTriangles(allTriangles,0,true);

            mesh.RecalculateBounds();
            if (meshy != null)
            {
                meshy.mesh = mesh;
            }

            UnityEngine.Debug.Log("Drawn mesh line");
            hasDrawnLines = true;
        }


        void drawLines(List<int> allIndices, List<Color> allColours, List<Vector3> allVerts)
        {
                Mesh mesh = new Mesh();
                mesh.subMeshCount = 1;
                //mesh.vertices = verts;

               // UnityEngine.Debug.Log("vertCount is " + allVerts.Count.ToString());
                mesh.SetVertices(allVerts.ToArray());
                mesh.SetColors(allColours.ToArray());
                mesh.SetIndices(allIndices.ToArray(), MeshTopology.Lines, 0);

                mesh.RecalculateBounds();
                if (meshy != null)
                {
                    meshy.mesh = mesh;
                }
            UnityEngine.Debug.Log("Drawn mesh line");
            hasDrawnLines = true;
        }


        Vector3 getPointOnTrack(double value, double max, double min, float theta, int track, Vector2 origin)
        {
            float startingOffset = 1;
            float trackWidth = 1;
            float r;
            float scaledValue;
            startingOffset = 1;
            trackWidth = 1;
            r = startingOffset + track * trackWidth + trackWidth / 2;
            scaledValue = (float)((value - min) / (max - min) - .5)*2;
            r = r + scaledValue * trackWidth / 2;

            return new Vector3( origin.x + r * Mathf.Sin(theta), 
                                origin.y + r * Mathf.Cos(theta),0);
        }
        void getLineOnTrack(double value, double max, double min, float theta, int track, Vector2 origin, out List<Vector3> vertsOut )
        {
            vertsOut = new List<Vector3>();

            float startingOffset = 1;
            float trackWidth = 2;
            float r;
            float scaledValue;
            r = startingOffset + track * trackWidth + trackWidth / 2;
            scaledValue = (float)((value - min) / (max - min) );
            float rEnd = r + scaledValue * trackWidth / 2;
            float rStart = r - scaledValue * trackWidth / 2;

            vertsOut.Add(new Vector3(origin.x + rEnd * Mathf.Sin(theta),
                                origin.y + rEnd * Mathf.Cos(theta), 0));
            vertsOut.Add(new Vector3(origin.x + rStart * Mathf.Sin(theta),
                                origin.y + rStart * Mathf.Cos(theta), 0));
        }

    }
}
