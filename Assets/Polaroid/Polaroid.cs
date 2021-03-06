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
	         
	         var meshVertices = mesh.vertices.ToList();
	         var meshTriangles = mesh.triangles;

	         var matchingVertices = GetVerticesInsideViewFrustum(staticObject, in meshVertices, planes);
	         if (matchingVertices.Count == 0) continue;
	         
	         var newTriangles = GetTrianglesInsideViewFrustrum(staticObject.transform, ref meshVertices, ref matchingVertices, in meshTriangles, in planes);
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

	private List<int> GetVerticesInsideViewFrustum(GameObject obj, in List<Vector3> meshVertices, Plane[] planes)
	{
		var matchingVertices = new List<int>();
		
		for (var i = 0; i < meshVertices.Count; i++)
		{
			var vertex = meshVertices[i];
			if (IsInsideFrustum(obj.transform.TransformPoint(vertex), planes))
			{
				matchingVertices.Add(i);
			}
		}
		
		return matchingVertices;
	}
	
	private List<int> GetTrianglesInsideViewFrustrum(Transform gameObjectTransform, ref List<Vector3> meshVertices, ref List<int> matchingVertices, in int[] meshTriangles, in Plane[] planes)
	{
		// iterate the triangle list (using i += 3) checking if
		// each vertex of the triangle is inside the frustum (using previously calculated matching vertices)
		var newTriangles = new List<int>();
		for (var i = 0; i < meshTriangles.Length; i += 3)
		{
			var contain = new[] {false, false, false}.ToList();
			for (var j = 0; j < 3; j++)
			{
				if (matchingVertices.Contains(meshTriangles[i + j]))
					contain[j] = true;
			}

			var count = contain.Count(x => x);
			if (count == 3)
			{
				newTriangles.AddRange(new[] {meshTriangles[i], meshTriangles[i + 1], meshTriangles[i + 2]});
			}

			if (count == 2)
			{
				var outsiderIndex = contain.IndexOf(false);
				var insiderIndex = contain.IndexOf(true);
				var insiderIndex2 = contain.LastIndexOf(true);
				var outsider = meshVertices[meshTriangles[i + outsiderIndex]];
				var insider = meshVertices[meshTriangles[i + insiderIndex]];
				var insider2 = meshVertices[meshTriangles[i + insiderIndex2]];

				Ray ray1 = new Ray(gameObjectTransform.TransformPoint(insider),
					gameObjectTransform.TransformPoint(outsider) - gameObjectTransform.TransformPoint(insider));
				Ray ray2 = new Ray(gameObjectTransform.TransformPoint(insider2),
					gameObjectTransform.TransformPoint(outsider) - gameObjectTransform.TransformPoint(insider2));
				foreach (var plane in planes)
				{
					if (!plane.GetSide(outsider))
					{
						if (plane.Raycast(ray1, out var enter) && plane.Raycast(ray2, out var enter2))
						{
							var point = gameObjectTransform.InverseTransformPoint(ray1.GetPoint(enter));
							var point2 = gameObjectTransform.InverseTransformPoint(ray2.GetPoint(enter2));
							newTriangles.AddRange(
								GetClockwiseRotation(
									new[] {outsiderIndex, insiderIndex, insiderIndex2},
									new[] {meshVertices.Count, meshTriangles[i + insiderIndex], meshTriangles[i + insiderIndex2]}
								)
							);
							matchingVertices.Add(meshVertices.Count);
							meshVertices.Add(point);
							newTriangles.AddRange(
								GetClockwiseRotation(
									new[] {outsiderIndex, insiderIndex, insiderIndex2},
									new[] {meshVertices.Count - 1, meshTriangles[i + insiderIndex2], meshVertices.Count}
								)
							);
							matchingVertices.Add(meshVertices.Count);
							meshVertices.Add(point2);
						}
					}
				}
			}
			if (count == 1)
			{
				var insiderIndex = contain.IndexOf(true);
				var outsiderIndex = contain.IndexOf(false);
				var outsiderIndex2 = contain.LastIndexOf(false);
				var insider = meshVertices[meshTriangles[i + insiderIndex]];
				var outsider = meshVertices[meshTriangles[i + outsiderIndex]];
				var outsider2 = meshVertices[meshTriangles[i + outsiderIndex2]];

				Ray ray1 = new Ray(gameObjectTransform.TransformPoint(insider),
					gameObjectTransform.TransformPoint(outsider) - gameObjectTransform.TransformPoint(insider));
				Ray ray2 = new Ray(gameObjectTransform.TransformPoint(insider),
					gameObjectTransform.TransformPoint(outsider2) - gameObjectTransform.TransformPoint(insider));
				foreach (var plane in planes)
				{
					if (!plane.GetSide(outsider))
					{
						if (plane.Raycast(ray1, out var enter) && plane.Raycast(ray2, out var enter2))
						{
							var point = gameObjectTransform.InverseTransformPoint(ray1.GetPoint(enter));
							var point2 = gameObjectTransform.InverseTransformPoint(ray2.GetPoint(enter2));
							newTriangles.AddRange(
								GetClockwiseRotation(
									new[] {insiderIndex, outsiderIndex, outsiderIndex2},
									new[] {meshTriangles[i + insiderIndex], meshVertices.Count, meshVertices.Count + 1}
								)
							);
							matchingVertices.AddRange(new []{meshVertices.Count, meshVertices.Count + 1});
							meshVertices.Add(point);
							meshVertices.Add(point2);
						}
					}
				}
			}
		}
		return newTriangles;
	}

	private int[] GetClockwiseRotation(int[] index, int[] triangles)
	{ 
		if (index[0] > index[1] && index[0] < index[2])
		{
			return new[] {triangles[1], triangles[0], triangles[2]};
		}
		return new[] {triangles[0], triangles[1], triangles[2]};
	}
	
	private void RemapVerticesAndTrianglesToNewMesh(in List<int> matchingVertices, in List<Vector3> meshVertices, ref List<int> newTriangles, out List<Vector3> newVertices)
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
