using System;
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
	private Material pictureMaterial;
	private Texture2D snappedPictureTexture;
	
	[SerializeField]
	private Camera cam;

	public List<GameObject> toBePlaced = new List<GameObject>();

	private List<Vector3> projectedPointsToDraw1 = new List<Vector3>();
	private List<Vector3> projectedPointsToDraw2 = new List<Vector3>();
	private List<Vector3> projectedPointsToDraw3 = new List<Vector3>();
	private List<(Vector3, Color)> projectedPointsToDraw4 = new List<(Vector3, Color)>();

	private void OnDrawGizmos()
	{
		foreach (var point in projectedPointsToDraw1)
		{
			var c = Gizmos.color;
			var inside = IsInsideFrustum(point, GeometryUtility.CalculateFrustumPlanes(cam));
			Gizmos.color = inside? Color.green : Color.red;
			Gizmos.DrawWireSphere(point, 0.05f);
			Gizmos.color = c;
		}
		
		foreach (var point in projectedPointsToDraw4)
		{
			var c = Gizmos.color;
			var inside = IsInsideFrustum(point.Item1, GeometryUtility.CalculateFrustumPlanes(cam));
			Gizmos.color = point.Item2;
			Gizmos.DrawWireSphere(point.Item1, 0.05f);
			Gizmos.color = c;
		}
		
		foreach (var point in projectedPointsToDraw2)
		{
			var c = Gizmos.color;
			var inside = IsInsideFrustum(point, GeometryUtility.CalculateFrustumPlanes(cam));
			Gizmos.color = inside? Color.green : Color.red;
			Gizmos.DrawWireCube(point, Vector3.one * 0.05f);
			Gizmos.color = c;
		}
		
		foreach (var point in projectedPointsToDraw3)
		{
			var c = Gizmos.color;
			var inside = IsInsideFrustum(point, GeometryUtility.CalculateFrustumPlanes(cam));
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireCube(point, Vector3.one * 0.05f);
			Gizmos.color = c;
		}
	}

	private void Start()
	{
		parentCamera = transform.parent;
		offsetFromParent = transform.localPosition;
		snappedPictureTexture = new Texture2D(pictureRenderTexture.width, pictureRenderTexture.height, pictureRenderTexture.graphicsFormat, TextureCreationFlags.None);
		pictureMaterial = picture.GetComponent<Renderer>().material;
	}
	
//	private void Update()
//	{
//		{
//			var planes = GeometryUtility.CalculateFrustumPlanes(cam);
//			for (var i = 0; i < planes.Length; i++)
//			{
//				var plane0 = planes[i];
//				for (var j = i+1; j < planes.Length; j++)
//				{
//					var plane1 = planes[j];
//					MathExtensions.PlanePlaneIntersection(out var linePoint, out var lineVec,
//						plane0.normal, plane0.normal * plane0.distance,
//						plane1.normal, plane1.normal * plane1.distance);
//
//					Debug.DrawLine(linePoint, lineVec,
//						Color.red);
//				}
//			}
//		}
//	}

	public void Place()
	{
		//frame.SetActive(false);
		snapShotSaved = false;
		
		foreach (var obj in toBePlaced)
		{
//			print(obj.ToString());
			obj.SetActive(true);
			obj.transform.SetParent(null);
		}
		toBePlaced.Clear();
		pictureMaterial.SetTexture("_UnlitColorMap", pictureRenderTexture);
	}

	public void Snapshot()
	{
	     toBePlaced.Clear();
	     projectedPointsToDraw1.Clear();
	     projectedPointsToDraw2.Clear();
	     projectedPointsToDraw3.Clear();
	     projectedPointsToDraw4.Clear();
	     
	     Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
	     var staticObjects = GameObject.FindGameObjectsWithTag("StaticObject");
	     var dynamicObjects = GameObject.FindGameObjectsWithTag("DynamicObject");
	     
	     foreach (var obj in staticObjects)
	     {
	         var mesh = obj.GetComponent<MeshFilter>().mesh;
//	         if (!GeometryUtility.TestPlanesAABB(planes, mesh.bounds)) continue;
	         
	         var meshVertices = mesh.vertices.ToList();
	         var meshTriangles = mesh.triangles;

	         var matchingVertices = GetVerticesInsideViewFrustum(obj.transform, in meshVertices, planes);
	         if (matchingVertices.Count == 0) continue;
	         
	         var newTriangles = GetTrianglesInsideViewFrustrum(obj.transform, ref meshVertices, ref matchingVertices, in meshTriangles, in planes);
			 RemapVerticesAndTrianglesToNewMesh(in matchingVertices, in meshVertices, ref newTriangles, out var newVertices);
			 PrepareCreateNewObject(obj, newVertices, newTriangles);
	     }
	     
	     snapShotSaved = true;
	     
	     Graphics.CopyTexture(pictureRenderTexture, snappedPictureTexture);
	     pictureMaterial.SetTexture("_UnlitColorMap", snappedPictureTexture);
	}

	private bool IsInsideFrustum(Vector3 point, IEnumerable<Plane> planes)
	{
		return planes.All(plane => plane.GetSide(point));
	}

	private List<int> GetVerticesInRelationToViewFrustum(Transform objTransform, in List<Vector3> meshVertices, Plane[] planes, bool inside)
	{
		var matchingVertices = new List<int>();
		
		for (var i = 0; i < meshVertices.Count; i++)
		{
			var vertex = meshVertices[i];
			if (IsInsideFrustum(objTransform.TransformPoint(vertex), planes) == inside)
				matchingVertices.Add(i);
		}
		
		return matchingVertices;
	}

	private List<int> GetVerticesOutsideViewFrustum(Transform gameObjectTransform, in List<Vector3> meshVertices, Plane[] planes)
	{
		return GetVerticesInRelationToViewFrustum(gameObjectTransform, meshVertices, planes, false);
	}
	
	private List<int> GetVerticesInsideViewFrustum(Transform gameObjectTransform, in List<Vector3> meshVertices, Plane[] planes)
	{
		return GetVerticesInRelationToViewFrustum(gameObjectTransform, meshVertices, planes, true);
	}
	private List<int> GetTrianglesInsideViewFrustrum(Transform gameObjectTransform, ref List<Vector3> meshVertices, ref List<int> matchingVertices, in int[] meshTriangles, in Plane[] planes)
	{
		// iterate the triangle list (using i += 3) checking if
		// each vertex of the triangle is inside the frustum (using previously calculated matching vertices)
		var newTriangles = new List<int>();
		for (var i = 0; i < meshTriangles.Length; i += 3)
		{
			var contain = new List<bool> {false, false, false};
			
			for(var j = 0; j < 3; j++)
				contain[j] = matchingVertices.Contains(meshTriangles[i + j]);

			var verticesInsideFrustumCount = contain.Count(x => x);
			
			// ReSharper disable once ConvertIfStatementToSwitchStatement
			if (verticesInsideFrustumCount == 3)
			{
				newTriangles.AddRange(new[] {meshTriangles[i], meshTriangles[i + 1], meshTriangles[i + 2]});
			}
			else if (verticesInsideFrustumCount == 2)
			{
				var outsiderIndex = contain.IndexOf(false);
				var insiderIndex = contain.IndexOf(true);
				var insiderIndex2 = contain.LastIndexOf(true);
				var outsider = meshVertices[meshTriangles[i + outsiderIndex]];
				var insider = meshVertices[meshTriangles[i + insiderIndex]];
				var insider2 = meshVertices[meshTriangles[i + insiderIndex2]];
				
				//if (planes.Count(x => x.GetSide(gameObjectTransform.TransformPoint(outsider))) > 5) continue;
				
				{
					var p = planes.Select(x => x.GetSide(outsider));
					var c = (planes.Count(x => x.GetSide(outsider)), planes.Count(x => x.GetSide(insider)), planes.Count(x => x.GetSide(insider2)));
					var s = string.Join(", ", p);
//					Debug.Log($"{gameObjectTransform.gameObject.name} ({outsiderIndex}, {insiderIndex}, {insiderIndex2})[{c}] - (" +
//					          $"{contain[outsiderIndex]}:{matchingVertices.Contains(meshTriangles[i + outsiderIndex])}, " +
//					          $"{contain[insiderIndex]}:{matchingVertices.Contains(meshTriangles[i + insiderIndex])}, " +
//					          $"{contain[insiderIndex2]}:{matchingVertices.Contains(meshTriangles[i + insiderIndex2])}) " +
//					          $"- {s}");
				}

				var planesVertexIsOutsideOf =
					planes.Where(x => !x.GetSide(gameObjectTransform.TransformPoint(outsider))).ToList();

				var planesVertexIsOutsideOfCount = planesVertexIsOutsideOf.Count();
				Debug.Log($"Outside {planesVertexIsOutsideOfCount}");
				if (planesVertexIsOutsideOfCount == 1)
				{
					var insiders = new[] {insider, insider2};
					var newPoints = new[] {outsider, outsider};

					foreach (var plane in planesVertexIsOutsideOf)
					{
						for (var p = 0; p < newPoints.Length; p++)
						{
							newPoints[p] = ProjectPointOnPlane(insiders[p], newPoints[p], plane,
								gameObjectTransform);
						}
					}

					foreach (var p in newPoints)
					{
						meshVertices.Add(p);
						matchingVertices.Add(meshVertices.Count - 1);
					}

					CreateTrapezoid(ref newTriangles, new[] {outsiderIndex, insiderIndex, insiderIndex2},
						new[] {meshVertices.Count - 1, meshTriangles[i + insiderIndex], meshTriangles[i + insiderIndex2]},
						new[] {meshVertices.Count - 2, meshTriangles[i + insiderIndex], meshVertices.Count - 1});
				}
				else if (planesVertexIsOutsideOfCount == 2)
				{
					var insiders = new[] {insider, insider2};
					var newPoints = new[] {outsider, outsider, outsider};
					
					foreach (var plane in planesVertexIsOutsideOf)
					{
						for (var p = 0; p < 2; p++)
						{
							newPoints[p] = ProjectPointOnPlane(insiders[p], newPoints[p], plane,
								gameObjectTransform);
						}
						
						newPoints[2] = ProjectPointOnPlane(insiders[0], newPoints[2], plane, gameObjectTransform);
						newPoints[2] = ProjectPointOnPlane(insiders[1], newPoints[2], plane, gameObjectTransform);
					}

					foreach (var p in newPoints)
					{
						meshVertices.Add(p);
						matchingVertices.Add(meshVertices.Count - 1);
					}

//					CreateTrapezoid(ref newTriangles, new[] {outsiderIndex, insiderIndex, insiderIndex2},
//						new[]{meshVertices.Count - 1, meshTriangles[i + insiderIndex], meshTriangles[i + insiderIndex2]},
//						new[] {meshVertices.Count - 2, meshTriangles[i + insiderIndex], meshVertices.Count - 1});
					
//					newTriangles.AddRange(new[] {meshVertices.Count - 3, meshVertices.Count - 2, meshVertices.Count - 1});
				}
			}
			else if (verticesInsideFrustumCount == 1)
			{
				var insiderIndex = contain.IndexOf(true);
				var outsiderIndex = contain.IndexOf(false);
				var outsiderIndex2 = contain.LastIndexOf(false);
				
				var insider = meshVertices[meshTriangles[i + insiderIndex]];
				var outsider = meshVertices[meshTriangles[i + outsiderIndex]];
				var outsider2 = meshVertices[meshTriangles[i + outsiderIndex2]];
				
				var outsiders = new []{outsider, outsider2};
				for (var p = 0; p < outsiders.Length; p++)
				{
					var planesVertexIsOutsideOf =
						planes.Where(x => !x.GetSide(gameObjectTransform.TransformPoint(outsiders[p]))).ToList();

					var planesVertexIsOutsideOfCount = planesVertexIsOutsideOf.Count();
					
					projectedPointsToDraw1.Add(gameObjectTransform.TransformPoint(insider));
					projectedPointsToDraw1.Add(gameObjectTransform.TransformPoint(outsider));
					projectedPointsToDraw1.Add(gameObjectTransform.TransformPoint(outsider2));

					if (planesVertexIsOutsideOfCount > 1)
					{
						var plane1 = planesVertexIsOutsideOf[0];
						var plane2 = planesVertexIsOutsideOf[1];
						var plane3 = new Plane();
						var o = GetClockwiseRotation(new[] {insiderIndex, outsiderIndex, outsiderIndex2}, new[]
						{
							meshTriangles[i + insiderIndex],
							meshTriangles[i + outsiderIndex],
							meshTriangles[i + outsiderIndex2]
						});
						plane3.Set3Points
						(
						gameObjectTransform.TransformPoint(meshVertices[o[0]]),
						gameObjectTransform.TransformPoint(meshVertices[o[1]]),
						gameObjectTransform.TransformPoint(meshVertices[o[2]])
						);

//							projectedPointsToDraw1.Add(gameObjectTransform.TransformPoint(meshVertices[o[0]]));
//							projectedPointsToDraw1.Add(gameObjectTransform.TransformPoint(meshVertices[o[1]]));
//							projectedPointsToDraw1.Add(gameObjectTransform.TransformPoint(meshVertices[o[2]]));
//						
						if (!MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane3, plane2, out outsiders[p]))
							MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane2, plane3, out outsiders[p]);
						
//						projectedPointsToDraw3.Add(outsiders[p]);
						outsiders[p] = gameObjectTransform.InverseTransformPoint(outsiders[p]);
					}
					
					else
					{
						var plane = planesVertexIsOutsideOf[0];
						outsiders[p] = ProjectPointOnPlane(insider, outsiders[p], plane, gameObjectTransform);
					}
				}
				
				foreach (var p in outsiders)
				{
					meshVertices.Add(p);
					matchingVertices.Add(meshVertices.Count - 1);
				}

				CreateTriangle(ref newTriangles, 
					new[] {insiderIndex, outsiderIndex, outsiderIndex2},  
					new[] {meshTriangles[i + insiderIndex], meshVertices.Count - 2, meshVertices.Count - 1});
			}
		}
		return newTriangles;
	}
	
	private Vector3 ProjectPointOnPlane(Vector3 inside, Vector3 outside, Plane plane, Transform objectTransform)
	{
		var origin = objectTransform.TransformPoint(inside);
		var direction = objectTransform.TransformPoint(outside) - objectTransform.TransformPoint(inside);
							
		var ray = new Ray(origin,direction);
		plane.Raycast(ray, out var enter);
		return objectTransform.InverseTransformPoint(ray.GetPoint(enter));
	}

	private void CreateTrapezoid(ref List<int> newTriangles, int[] indices, int[] t1, int[] t2)
	{
		CreateTriangle(ref newTriangles, indices,t1);
		CreateTriangle(ref newTriangles, indices,t2);
	}
	
	private void CreateTriangle(ref List<int> newTriangles, int[] indices, int[] triangle)
	{
		newTriangles.AddRange(GetClockwiseRotation(indices,triangle));
	}

	private int[] GetClockwiseRotation(int[] indices, int[] triangles)
	{ 
		if (indices[0] > indices[1] && indices[0] < indices[2])
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
		var newObject = Instantiate(obj, parent: parentCamera, position: obj.transform.position + offsetFromParent, rotation: obj.transform.rotation);
		newObject.transform.localPosition += offsetFromParent;
		newObject.SetActive(false);

		var newMesh = new Mesh();

		
		newMesh.SetVertices(newVertices);
		newMesh.SetTriangles(newTriangles.ToArray(), 0);
		newMesh.RecalculateNormals();
		newMesh.RecalculateBounds();
		newMesh.RecalculateTangents();

		newObject.GetComponent<MeshFilter>().mesh = newMesh;

		var col = newObject.GetComponent<MeshCollider>();
		if (col != null) col.sharedMesh = newMesh;
		toBePlaced.Add(newObject);
	}
}
