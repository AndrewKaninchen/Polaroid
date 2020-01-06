using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Polaroid : MonoBehaviour
{
	[Serializable]
	public class PolaroidObject
	{
		public GameObject gameObj;
		public Vector3 camDistance;
		public Quaternion camRelativeAngle;

		public PolaroidObject(GameObject obj, Vector3 distance, Quaternion angle)
		{
			this.gameObj = obj;
			this.camDistance = distance;
			this.camRelativeAngle = angle;
		}

	}
	[SerializeField]
	private Camera cam;

	public GameObject testObjectPrefab;
	public float vertexBoundsSize = 0.1f;
	public bool useAABB = true;
	public List<PolaroidObject> toBePlaced = new List<PolaroidObject>();

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F))
			useAABB = !useAABB;

		if (Input.GetKeyDown(KeyCode.Space))
		{
			Snapshot();
		}

		if (Input.GetKeyDown(KeyCode.Backspace))
			Place();
	}

	private void Place()
	{
		foreach (var obj in toBePlaced)
		{
			print(obj.ToString());
			Destroy(obj.gameObj);
			Instantiate(obj.gameObj, cam.transform.position + obj.camDistance, obj.camRelativeAngle);
		}
		toBePlaced.Clear();
	}
	
	private void Snapshot()
    {
         toBePlaced.Clear();
         Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
         var staticObjects = GameObject.FindGameObjectsWithTag("StaticObject");
         var dynamicObjects = GameObject.FindGameObjectsWithTag("DynamicObject");
         
         foreach (var staticObject in staticObjects)
         {
             var mesh = staticObject.GetComponent<MeshFilter>().mesh;
             var meshVertices = mesh.vertices;
             var meshTriangles = mesh.triangles;

             var matchingVertices = GetVerticesInsideViewFrustum(staticObject, meshVertices, planes);
             var newTriangles = GetTrianglesInsideViewFrustrum(mesh, matchingVertices, in meshTriangles);
			 RemapVerticesAndTrianglesToNewMesh(in matchingVertices, in meshVertices, ref newTriangles, out var newVertices);
			 PrepareCreateNewObject(staticObject, newVertices, newTriangles);
         }
	}

	private bool IsInsideFrustum(Vector3 point, IEnumerable<Plane> planes)
	{
		return planes.All(plane => plane.GetSide(point));
	}

	private List<int> GetVerticesInsideViewFrustum(GameObject obj, Vector3[] meshVertices, Plane[] planes)
	{
		var matchingVertices = new List<int>();
		
		for (var i = 0; i < meshVertices.Length; i++)
		{
			var vertex = meshVertices[i];

			if (useAABB)
			{
				var bounds = new Bounds(obj.transform.TransformPoint(vertex), Vector3.one * vertexBoundsSize);
				if (GeometryUtility.TestPlanesAABB(planes, bounds))
					matchingVertices.Add(i);
			}
			else
			{
				if(IsInsideFrustum(obj.transform.TransformPoint(vertex), planes))
					matchingVertices.Add(i);
			}
			
		}
		
		return matchingVertices;
	}
	
	private List<int> GetTrianglesInsideViewFrustrum(Mesh mesh, List<int> matchingVertices, in int[] meshTriangles)
	{
		// iterate the triangle list (using i += 3) checking if
		// each vertex of the triangle is inside the frustum (using previously calculated matching vertices)
		var newTriangles = new List<int>();
		for (int i = 0; i < meshTriangles.Length; i += 3)
		{
			//Debug.LogFormat("i: {0}, point[i]: {1}, point[i+1]: {2}, point[i+2]: {3}", i, mesh.triangles[i], mesh.triangles[i + 1], mesh.triangles[i + 2]);
			if (matchingVertices.Contains(meshTriangles[i]) &&
			    matchingVertices.Contains(meshTriangles[i + 1]) &&
			    matchingVertices.Contains(meshTriangles[i + 2]))
			{
				newTriangles.AddRange(new[] {meshTriangles[i], meshTriangles[i + 1], meshTriangles[i + 2]});
			}
		}

		return newTriangles;
	}
	
	private void RemapVerticesAndTrianglesToNewMesh(in List<int> matchingVertices, in Vector3[] meshVertices, ref List<int> newTriangles, out List<Vector3> newVertices)
	{
		newVertices = new List<Vector3>();
		foreach (var i in matchingVertices)
		{
			newVertices.Add(meshVertices[i]);
		}
		
		var dictionary = new Dictionary<int, int>();
		for (int i = 0; i < matchingVertices.Count; i++)
		{
			dictionary.Add(matchingVertices[i], i);
		}
	
		for (int i = 0; i < newTriangles.Count; i++)
		{
			newTriangles[i] = dictionary[newTriangles[i]];
		}
	}

	private void PrepareCreateNewObject(GameObject obj, List<Vector3> newVertices, List<int> newTriangles)
	{
		var newObject = Instantiate(obj);
		//var newObject = Instantiate(testObjectPrefab);
		var newMesh = new Mesh();

		newMesh.SetVertices(newVertices);
		newMesh.SetTriangles(newTriangles.ToArray(), 0);
		newObject.GetComponent<MeshFilter>().mesh = newMesh;
		newMesh.RecalculateNormals();
		toBePlaced.Add(new PolaroidObject(newObject, obj.transform. position - cam.transform.position, Quaternion.identity));
	}
}
