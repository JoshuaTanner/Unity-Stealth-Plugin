/*
* This stealth plugin tooklit was made by:
* Joshua Tanner
* Dylan Meissner
* Zac Watson
* Media Design School 2017
*/

using UnityEngine;
using UnityEditor;
using StealthPlugin;
using PathBuilder;

namespace StealthEditor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Stealth))]
    public class StealthEditor : Editor
    {
        public override void OnInspectorGUI()
        {

        Stealth stealthScript = (Stealth)target;

            DrawDefaultInspector();

            //creates patrol path
            if (GUILayout.Button("Create patrol path"))
            {
                if (stealthScript.pathParent == null)
                {
                    // set up the path parent
                    stealthScript.pathParent = new GameObject();
                    stealthScript.pathParent.transform.position = stealthScript.gameObject.transform.position;
                    stealthScript.pathParent.name = "Path Controller";
                    stealthScript.pathParent.transform.parent = stealthScript.gameObject.transform;
                    PathController controller = stealthScript.pathParent.AddComponent<PathController>();
                }

                //make the path parent the selected 
                Selection.activeGameObject = stealthScript.pathParent;
            }   
        }      
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(DetectionMeter))]
    public class DetectionMeterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            //create detection meter button
            DetectionMeter detectionScript = (DetectionMeter)target;
            if (GUILayout.Button("Add Detection Meter"))
            {
                detectionScript.CreateDetectionMeter();
            }
        }
    }
    
}
