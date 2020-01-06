﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.UI;
using UnityEngine.Serialization;

public class Polaroid : MonoBehaviour
{
	private Transform parentCamera;
	public Vector3 offsetFromParent;

	private bool snapShotSaved = false;
	[FormerlySerializedAs("renderTexture")] public RenderTexture pictureRenderTexture;
	public GameObject picture;
	public GameObject frame;
	private Material pictureMaterial;
	private Texture2D snappedPictureTexture;
	
	[SerializeField]
	private Camera cam;

	public List<GameObject> toBePlaced = new List<GameObject>();

	private void Start()
	{
		parentCamera = transform.parent;
		offsetFromParent = transform.localPosition;
		snappedPictureTexture = new Texture2D(pictureRenderTexture.width, pictureRenderTexture.height, pictureRenderTexture.graphicsFormat, TextureCreationFlags.None);
		pictureMaterial = picture.GetComponent<Renderer>().material;
	}
	
	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			if (frame.activeSelf)
			{
				if (!snapShotSaved)
				{
					Snapshot();
				}
				else
				{
					Place();
				}
			}
			else
				frame.SetActive(true);
		}
	}

	private void Place()
	{
		frame.SetActive(false);
		snapShotSaved = false;
		
		foreach (var obj in toBePlaced)
		{
			print(obj.ToString());
			obj.SetActive(true);
			//obj.transform.localPosition += frame.transform.localPosition;
			obj.transform.SetParent(null);
		}
		toBePlaced.Clear();
		pictureMaterial.SetTexture("_UnlitColorMap", pictureRenderTexture);
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
	         //if (!GeometryUtility.TestPlanesAABB(planes, mesh.bounds)) continue;
	         
	         var meshVertices = mesh.vertices;
	         var meshTriangles = mesh.triangles;

	         var matchingVertices = GetVerticesInsideViewFrustum(staticObject, meshVertices, planes);
	         if (matchingVertices.Count == 0) continue;
	         
	         var newTriangles = GetTrianglesInsideViewFrustrum(mesh, matchingVertices, in meshTriangles);
			 RemapVerticesAndTrianglesToNewMesh(in matchingVertices, in meshVertices, ref newTriangles, out var newVertices);
			 PrepareCreateNewObject(staticObject, newVertices, newTriangles);
	     }
	     
	     snapShotSaved = true;
	     
	     Graphics.CopyTexture(pictureRenderTexture, snappedPictureTexture);
	     pictureMaterial.SetTexture("_UnlitColorMap", snappedPictureTexture);
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
			if(IsInsideFrustum(obj.transform.TransformPoint(vertex), planes))
				matchingVertices.Add(i);
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
		var newObject = Instantiate(obj, parent: parentCamera, position: obj.transform.position, rotation: obj.transform.rotation);
		newObject.transform.localPosition += offsetFromParent;
		newObject.SetActive(false);

		var newMesh = new Mesh();

		newMesh.SetVertices(newVertices);
		newMesh.SetTriangles(newTriangles.ToArray(), 0);
		newObject.GetComponent<MeshFilter>().mesh = newMesh;
		newMesh.RecalculateNormals();
		
		toBePlaced.Add(newObject);
		
	}
}
