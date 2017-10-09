/*
* This stealth plugin tooklit was made by:
* Joshua Tanner
* Dylan Meissner
* Zac Watson
* Media Design School 2017
*/

using UnityEditor;
using UnityEngine;
using PathBuilder;

namespace PathEditor
{
    [InitializeOnLoad]
    [CustomEditor(typeof(PathPoint))]
    public class PathPointEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PathPoint pointScript = (PathPoint)target;    

            // add next point button
            if (GUILayout.Button("Add next point"))
            {      
                // call the add point function
                Undo.RecordObject(pointScript, "Undo add point");
                GameObject newPoint = pointScript.pointController.InsertPoint(pointScript);
                Selection.activeGameObject = newPoint;              
            }                    
        }   
    }


     [CustomEditor(typeof(PathController))]
    public class PathControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PathController controllerScript = (PathController)target;

            if (GUILayout.Button("Create path point"))
            {
                GameObject newPoint = controllerScript.AddPoint();
                Selection.activeGameObject = newPoint;
            }              

            //Check if path point or controller is selected
            if (controllerScript.gameObject == Selection.activeGameObject ||
                controllerScript.gameObject == Selection.activeGameObject.transform.parent.gameObject)
            {
                //set active to true so gizmos will render
                controllerScript.activePath = true;           
            }
            else
            {
                //set active to false so gizmos will not render
                controllerScript.activePath = false;               
            }         
            
        }

    }
  
    // finds the selected path and renders buttons
    // for path points and gizmos
    [InitializeOnLoad]
    class PathIcon
    {     
        static PathController activeController;

        static PathIcon()
        {
            EditorApplication.update += Update;

            SceneView.onSceneGUIDelegate -= OnScene;
            SceneView.onSceneGUIDelegate += OnScene;
        }

        private static void OnScene(SceneView sceneview)
        {
            if (activeController != null && activeController.listPoints != null && activeController.listPoints.Count > 0)
            {
                foreach (PathPoint point in activeController.listPoints)
                {
                    // draw handles
                    Handles.BeginGUI();          

                    Vector3 buttonScreenPos = Camera.current.WorldToScreenPoint(point.position);

                    Rect rect = new Rect(buttonScreenPos.x - 30.0f, Screen.height - buttonScreenPos.y - 60.0f, 20, 20);

                    //add point button
                    if (GUI.Button(rect, "+"))
                    {
                        Undo.RecordObject(point, "Undo add point");
                        GameObject newPoint = point.pointController.InsertPoint(point);
                        Selection.activeGameObject = newPoint;
                        break;
                    }

                    rect = new Rect(buttonScreenPos.x - 30.0f, Screen.height - buttonScreenPos.y - 35.0f, 20, 20);

                    //select point button
                    if (GUI.Button(rect, "S"))
                    {
                        Selection.activeGameObject = point.gameObject;
                        break;
                    }

                    Handles.EndGUI();
                }
            }
        }

        static void Update()
        {
            //search for path controllers in scene
            PathController[] controllers = GameObject.FindObjectsOfType<PathController>(); 

            activeController = null;

            //find the selected path controller or path point
            if (controllers.Length > 0 && Selection.activeGameObject)
            {              
                foreach (PathController controller in controllers)
                {
                   if (Selection.activeGameObject == controller.gameObject)
                   {
                        activeController = controller;
                        break;
                   }
                    else if (Selection.activeGameObject.transform.parent != null
                         && Selection.activeGameObject.transform.parent == controller.transform)
                    {
                        activeController = controller;
                        break;
                    }
                }
            }
        }
    }

}
