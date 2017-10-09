/*
* This stealth plugin tooklit was made by:
* Joshua Tanner
* Dylan Meissner
* Zac Watson
* Media Design School 2017
*/

using System.Collections.Generic;
using UnityEngine;

namespace PathBuilder
{    
    [System.Serializable]
    [ExecuteInEditMode]
    public class PathPoint : MonoBehaviour
    {
        // path point variables
        public PathController pointController; 
        public Vector3 position;
        public float rotation;
        public float wait;
           

        //Execute in edit mode callback to remove from controller list
        private void OnDestroy()
        {
            if(pointController != null)
            {
                pointController.RemovePoint(this);
            }
        }        

        private void OnDrawGizmosSelected()
        {
            pointController.DrawShapes();            
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                position = gameObject.transform.position;
            }
        }

    }

    // path controller stores and adds path points
    public class PathController : MonoBehaviour
    { 
        public bool activePath = false;

        public List<PathPoint> listPoints;

        public enum PathType { LOOP, PINGPONG };
        public PathType pathType;

        // add a new point to the end of the list
        public GameObject AddPoint()
        {
            GameObject pointGO = new GameObject();
            pointGO.name = "Path Point";
            pointGO.transform.parent = gameObject.transform;
            pointGO.transform.position = gameObject.transform.position;

            PathPoint newPathPoint = pointGO.AddComponent<PathPoint>();
            newPathPoint.pointController = this;
            newPathPoint.position = gameObject.transform.parent.transform.position;
            newPathPoint.rotation = 0.0f;
            newPathPoint.wait = 0.0f;

            if(listPoints == null)
            {
                listPoints = new List<PathPoint>();
            }

            listPoints.Add(newPathPoint);

            return pointGO;
        }

        // add a new point after a path point
        public GameObject InsertPoint(PathPoint lastPathPoint)
        {
            GameObject pointGO = new GameObject();
            pointGO.name = "Path Point";
            pointGO.transform.parent = gameObject.transform;

            PathPoint newPathPoint = pointGO.AddComponent<PathPoint>();
            newPathPoint.pointController = this;
       
            // set wait and rotation to 0.0
            newPathPoint.rotation = 0.0f;
            newPathPoint.wait = 0.0f;

            if (listPoints == null)
            {
                listPoints = new List<PathPoint>();
            }

            //check if last path point exists
            if(listPoints.Contains(lastPathPoint))
            {              
                newPathPoint.position = lastPathPoint.gameObject.transform.position;
                pointGO.transform.position = newPathPoint.position;
                int index = listPoints.IndexOf(lastPathPoint);
                listPoints.Insert(index + 1, newPathPoint);

            }
            else
            {
                pointGO.transform.position = gameObject.transform.position;
                newPathPoint.position = gameObject.transform.parent.transform.position;
                listPoints.Add(newPathPoint);
            }

            return pointGO;
        }

        // remove path point from list
        public void RemovePoint(PathPoint point)
        {
            listPoints.Remove(point);
        }  

        // draw the path spheres and lines
        public void DrawShapes()
        {
            if (listPoints != null && listPoints.Count > 0)
            {
                PathPoint previousPoint = null;

                foreach (PathPoint point in listPoints)
                {
                    //Draw line from point to previous point
                    if (previousPoint != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(point.position, previousPoint.position);
                    }

                    previousPoint = point;
                    
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(point.position, 0.2f);

                    //Draw the line showing direction of rotation
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(point.position, point.position + (Quaternion.AngleAxis(point.rotation, Vector3.up) * Vector3.forward) * 3.0f);
                }
                
                Gizmos.color = Color.yellow;
                if (pathType == PathType.LOOP)
                {
                    Gizmos.DrawLine(listPoints[listPoints.Count - 1].position, listPoints[0].position);
                }
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            DrawShapes();                         
        }    
    }
}
