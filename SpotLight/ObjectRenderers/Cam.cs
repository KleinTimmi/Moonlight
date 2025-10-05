using BYAML;
using GL_EditorFramework;
using GL_EditorFramework.EditorDrawables;
using GL_EditorFramework.GL_Core;
using GL_EditorFramework.Interfaces;
using GL_EditorFramework.StandardCameras;
using OpenTK; // Für Vector3, Vector4
using OpenTK.Graphics.OpenGL;
using Spotlight;
using Spotlight.EditorDrawables;
using Spotlight.GUI;
using Spotlight.Level;
using Spotlight.ObjectRenderers;
using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SZS;


namespace Moonlight.ObjectRenderers
{
    internal class Cam
    {

        public static void setupCameraLoad(SM3DWorldZone MapFile)
        {
            string mapFileName = MapFile.ToString() + "Map.szs";
            string modelPath = Program.TryGetPathViaProject("StageData", mapFileName);

            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Map file not found: {modelPath}");
                return;
            }

            byte[] fileData = File.ReadAllBytes(modelPath);
            var Map = SARCExt.SARC.UnpackRamN(YAZ0.Decompress(fileData));

            if (!Map.Files.ContainsKey("CameraParam.byml"))
                return;

            dynamic initModel = ByamlFile.FastLoadN(
                new MemoryStream(Map.Files["CameraParam.byml"]),
                false,
                ByteOrder.BigEndian
            ).RootNode;

            if (initModel is Dictionary<string, dynamic> initDict &&
                initDict.TryGetValue("Tickets", out var CameraTicketsVal) &&
                CameraTicketsVal is IEnumerable<object> ticketList)
            {
                foreach (var item in ticketList)
                {
                    if (item is IDictionary<string, object> dict)
                    {
                        string className = TryGetNestedString(dict, "Class", "Name");
                        string distance = TryGetAnyParam(dict, "Distance", "DistanceCurveName");
                        string angleV = TryGetAnyParam(dict, "AngleV", "StartAngleDegreeV", "AngleDegreeV");
                        string angleH = TryGetAnyParam(dict, "AngleH", "StartAngleDegreeH", "AngleDegreeH");
                        string lookAtOffset = TryGetVector(dict, "LookAtOffset", "LookAtPos");
                        string objId = TryGetNestedString(dict, "Id", "ObjId");

                        Vector3 position = ExtractPosition(dict); // Position auslesen

                        var ticketObj = new CameraTicketObject(position)
                        {
                            ClassName = "Camera", // <-- Wichtig für das Gizmo
                            ObjId = objId,
                            Distance = distance,
                            AngleV = angleV,
                            LookAtOffset = position
                        };

                        // Objekt zur Zone hinzufügen
                        


                        Debug.WriteLine($"Ticket:");
                        Debug.WriteLine($"  Class Name: {className}");
                        Debug.WriteLine($"  ObjId: {objId}");
                        Debug.WriteLine($"  Distance: {distance}");
                        Debug.WriteLine($"  AngleV: {angleV}");
                        Debug.WriteLine($"  AngleH: {angleH}");
                        Debug.WriteLine($"  LookAtOffset: {lookAtOffset}");
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid ticket format: {FormatDynamic(item)}");
                    }
                }

            }
            else
            {
                Debug.WriteLine("Tickets could not be loaded. Dumping initModel contents:");

                if (initModel is Dictionary<string, dynamic> fallbackDict)
                {
                    foreach (var kvp in fallbackDict)
                    {
                        Debug.WriteLine($"Key: {kvp.Key}");
                        Debug.WriteLine($"Value: {FormatDynamic(kvp.Value)}");
                    }
                }
                else
                {
                    Debug.WriteLine("initModel is not a dictionary – dump not possible.");
                }
            }
        }

        private static string TryGetNestedString(IDictionary<string, object> parent, string outerKey, string innerKey)
        {
            if (parent.TryGetValue(outerKey, out var outerObj))
            {
                if (outerObj is IDictionary<string, object> innerDict &&
                    innerDict.TryGetValue(innerKey, out var value))
                {
                    return value?.ToString() ?? "(null)";
                }
            }
            return "(not found)";
        }

        private static string TryGetNestedVector(IDictionary<string, object> parent, string outerKey, string innerKey)
        {
            if (parent.TryGetValue(outerKey, out var outerObj) &&
                outerObj is IDictionary<string, object> innerDict &&
                innerDict.TryGetValue(innerKey, out var vectorObj) &&
                vectorObj is IDictionary<string, object> vectorDict)
            {
                string x = vectorDict.TryGetValue("X", out var xVal) ? xVal?.ToString() ?? "?" : "?";
                string y = vectorDict.TryGetValue("Y", out var yVal) ? yVal?.ToString() ?? "?" : "?";
                string z = vectorDict.TryGetValue("Z", out var zVal) ? zVal?.ToString() ?? "?" : "?";
                return $"X: {x}, Y: {y}, Z: {z}";
            }
            return "(not found)";
        }

        private static string TryGetAnyParam(IDictionary<string, object> parent, params string[] keys)
        {
            if (parent.TryGetValue("Param", out var paramObj) &&
                paramObj is IDictionary<string, object> paramDict)
            {
                foreach (var key in keys)
                {
                    if (paramDict.TryGetValue(key, out var value))
                        return value?.ToString() ?? "(null)";
                }
            }
            return "(not found)";
        }

        private static string TryGetVector(IDictionary<string, object> parent, params string[] keys)
        {
            if (parent.TryGetValue("Param", out var paramObj) &&
                paramObj is IDictionary<string, object> paramDict)
            {
                foreach (var key in keys)
                {
                    if (paramDict.TryGetValue(key, out var vectorObj) &&
                        vectorObj is IDictionary<string, object> vectorDict)
                    {
                        string x = vectorDict.TryGetValue("X", out var xVal) ? xVal?.ToString() ?? "?" : "?";
                        string y = vectorDict.TryGetValue("Y", out var yVal) ? yVal?.ToString() ?? "?" : "?";
                        string z = vectorDict.TryGetValue("Z", out var zVal) ? zVal?.ToString() ?? "?" : "?";
                        return $"X: {x}, Y: {y}, Z: {z}";
                    }
                }
            }
            return "(not found)";
        }


        private static string FormatDynamic(object obj)
        {
            if (obj is IDictionary<string, object> dict)
                return string.Join(", ", dict.Select(kvp => $"{kvp.Key}: {FormatDynamic(kvp.Value)}"));
            else if (obj is IEnumerable<object> list)
                return "[" + string.Join(", ", list.Select(FormatDynamic)) + "]";
            else
                return obj?.ToString() ?? "null";
        }
        private static Vector3 ExtractPosition(IDictionary<string, object> dict)
        {
            if (dict.TryGetValue("Param", out var paramObj) &&
                paramObj is IDictionary<string, object> paramDict)
            {
                string[] keys = { "LookAtOffset", "LookAtPos", "CameraPos" };
                foreach (var key in keys)
                {
                    if (paramDict.TryGetValue(key, out var vectorObj) &&
                        vectorObj is IDictionary<string, object> vectorDict)
                    {
                        float x = float.TryParse(vectorDict["X"]?.ToString(), out var xVal) ? xVal : 0;
                        float y = float.TryParse(vectorDict["Y"]?.ToString(), out var yVal) ? yVal : 0;
                        float z = float.TryParse(vectorDict["Z"]?.ToString(), out var zVal) ? zVal : 0;
                        return new Vector3(x, y, z);
                    }
                }
            }
            return Vector3.Zero;
        }


    }
    public  class CameraTicketObject : TransformableObject
    {
        public string ClassName { get; set; }
        public string ObjId { get; set; }
        public string Distance { get; set; }
        public string AngleV { get; set; }
        public Vector3 LookAtOffset { get; set; }

        public CameraTicketObject(Vector3 position)
            : base(position, Vector3.Zero, Vector3.One) // Position, Rotation, Scale
        {

        }


        public override void Draw(GL_ControlModern control, Pass pass, EditorSceneBase editorScene)
        {
            // Spotlight verwendet transformedGlobalPos für Gizmo-Matrix
            
            Vector3 transformedGlobalPos = Selected ? editorScene.SelectionTransformAction.NewPos(GlobalPosition) : GlobalPosition;

            if (GizmoRenderer.TryDraw(ClassName, control, pass, transformedGlobalPos, new Vector4(1f, 0.5f, 0f, 1f)
))
                return;
        }

    }

    public static class DrawHelper
    {
        public static void DrawSphere(Vector3 position, float radius, Vector4 color)
        {
            GL.Color4(color);
            GL.PointSize(radius);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(position);
            GL.End();
        }


        public static void DrawText(GL_ControlModern control, Vector3 position, string text)
        {
            // Falls du ein Textsystem hast (z. B. BitmapFont), hier einfügen
            // Sonst ignorieren oder durch Tooltip ersetzen
        }
    }



}

