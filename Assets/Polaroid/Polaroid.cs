using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Polaroid : MonoBehaviour
{
	[SerializeField]
	private Camera cam;

	public GameObject testObjectPrefab;
	public float vertexBoundsSize = 0.1f;
	private void Update()
	{
		//if(Input.GetKey(KeyCode.Space))
			Snapshot();
	}

	private void Snapshot()
    {
         Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
         var staticObjects = GameObject.FindGameObjectsWithTag("StaticObject");
         var dynamicObjects = GameObject.FindGameObjectsWithTag("DynamicObject");
         
         foreach (var staticObject in staticObjects)
         {
             var mesh = staticObject.GetComponent<MeshFilter>().mesh;
             var meshVertices = mesh.vertices;
             var meshTriangles = mesh.triangles;

             var matchingVertices = GetVerticesInsideViewFrustrum(meshVertices, planes);
             var newTriangles = GetTrianglesInsideViewFrustrum(mesh, matchingVertices, in meshTriangles);
			 RemapVerticesAndTrianglesToNewMesh(in matchingVertices, in meshVertices, ref newTriangles, out var newVertices);
			 CreateNewObject(newVertices, newTriangles);
         }
	}

	private List<int> GetTrianglesInsideViewFrustrum(Mesh mesh, List<int> matchingVertices, in int[] meshTriangles)
	{
		// iterate in the triangle list (using i += 3) and checking if
		// each vertex of the triangle are inside the frustrum (using previously generated matching vertices)
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

	private List<int> GetVerticesInsideViewFrustrum(Vector3[] meshVertices, Plane[] planes)
	{
		var matchingVertices = new List<int>();
		
		for (var i = 0; i < meshVertices.Length; i++)
		{
			var vertex = meshVertices[i];
			
			var bounds = new Bounds(vertex, Vector3.one * vertexBoundsSize);
			if (GeometryUtility.TestPlanesAABB(planes, bounds))
				matchingVertices.Add(i);
		}

		return matchingVertices;
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

	private void CreateNewObject(List<Vector3> newVertices, List<int> newTriangles)
	{
		var newObject = testObjectPrefab;
		//var newObject = Instantiate(testObjectPrefab);
		var newMesh = new Mesh();

		newMesh.SetVertices(newVertices);
		newMesh.SetTriangles(newTriangles.ToArray(), 0);
		newObject.GetComponent<MeshFilter>().mesh = newMesh;
		newMesh.RecalculateNormals();
	}
}
