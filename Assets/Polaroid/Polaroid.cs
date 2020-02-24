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

	[FormerlySerializedAs("renderTexture")] public RenderTexture pictureRenderTexture;
	public GameObject picture;
	private Material pictureMaterial;
	private Texture2D snappedPictureTexture;
	
	[SerializeField]
	private Camera cam;

	public List<GameObject> toBePlaced = new List<GameObject>();

	private List<(Vector3, Color)> pointsToDraw = new List<(Vector3, Color)>();
	private static readonly int UnlitColorMap = Shader.PropertyToID("_UnlitColorMap");

	public struct PartiallyMatchingTriangle
	{
		public List<int> matchingVerticesIndices;
		public List<(int index, List<Plane> notMatchingPlanes)> notMatchingVertices;

		public PartiallyMatchingTriangle(List<int> matchingVerticesIndices, List<(int index, List<Plane> notMatchingPlanes)> notMatchingVertices)
		{
			this.matchingVerticesIndices = matchingVerticesIndices;
			this.notMatchingVertices = notMatchingVertices;
		}
	}
	public struct VerticesAndTrianglesInRelationToViewFrustum
	{
		public List<int> matchingVerticesIndices;
		public List<int> matchingTriangles;
		public List<PartiallyMatchingTriangle> partiallyMatchingTriangles;

		public VerticesAndTrianglesInRelationToViewFrustum(List<int> matchingVerticesIndices, List<int> matchingTriangles, List<PartiallyMatchingTriangle> partiallyMatchingTriangles)
		{
			this.matchingTriangles = matchingTriangles;
			this.matchingVerticesIndices = matchingVerticesIndices;
			this.partiallyMatchingTriangles = partiallyMatchingTriangles;
		}
	}
	
	private void OnDrawGizmos()
	{
		foreach (var point in pointsToDraw)
		{
			var c = Gizmos.color;
			var inside = IsInsideFrustum(point.Item1, GeometryUtility.CalculateFrustumPlanes(cam));
			Gizmos.color = point.Item2;
			Gizmos.DrawWireSphere(point.Item1, 0.05f);
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

	public void Place()
	{
		foreach (var obj in toBePlaced)
		{
			obj.SetActive(true);
			obj.transform.SetParent(null);
		}
		toBePlaced.Clear();
		pictureMaterial.SetTexture(UnlitColorMap, pictureRenderTexture);
	}

	public void Snapshot()
	{
	     toBePlaced.Clear();
	     pointsToDraw.Clear();
	     
	     var planes = GeometryUtility.CalculateFrustumPlanes(cam);
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
	         
	         var newTriangles = GetTrianglesInsideViewFrustrum(obj.transform, ref meshVertices, ref matchingVertices, meshTriangles, in planes);
//	         var verticesAndTrianglesInsideViewFrustum = GetVerticesAndTrianglesInsideViewFrustum(obj.transform, in meshVertices, meshTriangles, planes);
//	         var newTriangles = PreparateNewMesh(verticesAndTrianglesInsideViewFrustum, in meshVertices, in meshTriangles, obj.transform);
//		     RemapVerticesAndTrianglesToNewMesh(in verticesAndTrianglesInsideViewFrustum.matchingVerticesIndices, in meshVertices, ref newTriangles, out var newVertices);
		     RemapVerticesAndTrianglesToNewMesh(in matchingVertices, in meshVertices, ref newTriangles, out var newVertices);
			 PrepareCreateNewObject(obj, newVertices, newTriangles);
	     }
	     
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
	
	private VerticesAndTrianglesInRelationToViewFrustum GetVerticesAndTrianglesInRelationToViewFrustum(Transform objTransform, in List<Vector3> meshVertices, in List<int> meshTriangles, Plane[] planes, bool inside)
	{
		var verticesInRelationToViewFrustum = new VerticesAndTrianglesInRelationToViewFrustum(
			new List<int>(), 
			new List<int>(), 
			new List<PartiallyMatchingTriangle>());
		
		var notMatchingVertices = new List<(int index, List<Plane> notMatchingPlanes)>();
		for (var i = 0; i < meshVertices.Count; i++)
		{
			var vertex = meshVertices[i];
			var transformedVertex = objTransform.TransformPoint(vertex);
			var matchingPlanes = new List<Plane>();
			var notMatchingPlanes = new List<Plane>();
			
			foreach (var plane in planes)
			{
				if (plane.GetSide(transformedVertex) == inside)
					matchingPlanes.Add(plane);
				else
					notMatchingPlanes.Add(plane);
			}

			if (notMatchingPlanes.Count == 0)
				verticesInRelationToViewFrustum.matchingVerticesIndices.Add(i);
			else
				notMatchingVertices.Add((i, notMatchingPlanes));
		}

		for (var i = 0; i < meshTriangles.Count; i += 3)
		{
			var triangle = new PartiallyMatchingTriangle(new List<int>(), new List<(int index, List<Plane> notMatchingPlanes)>());

			for (var v = i; v <= i + 3; v++)
			{
				if (verticesInRelationToViewFrustum.matchingVerticesIndices.Contains(v))
					triangle.matchingVerticesIndices.Add(v);
				else
					triangle.notMatchingVertices.Add(notMatchingVertices.Find((x)=> x.index == v));
			}

			if (triangle.notMatchingVertices.Count == 0)
				verticesInRelationToViewFrustum.matchingTriangles.AddRange(triangle.matchingVerticesIndices);
			
			else
			{
				verticesInRelationToViewFrustum.partiallyMatchingTriangles.Add(triangle);
			}
		}

		return verticesInRelationToViewFrustum;
	}

	private VerticesAndTrianglesInRelationToViewFrustum GetVerticesAndTrianglesOutsideViewFrustum(Transform objTransform, in List<Vector3> meshVertices, in List<int> meshTriangles, Plane[] planes) 
	{
		return GetVerticesAndTrianglesInRelationToViewFrustum(objTransform, meshVertices, meshTriangles, planes, false);
	}

	private VerticesAndTrianglesInRelationToViewFrustum GetVerticesAndTrianglesInsideViewFrustum(Transform objTransform, in List<Vector3> meshVertices, in List<int> meshTriangles, Plane[] planes)
	{
		return GetVerticesAndTrianglesInRelationToViewFrustum(objTransform, meshVertices, meshTriangles, planes, true);
	}

	private List<int> PreparateNewMesh(VerticesAndTrianglesInRelationToViewFrustum verticesAndTrianglesInRelationToViewFrustum, in List<Vector3> meshVertices, in List<int> meshTriangles, Transform objTransform)
	{
		var matchingTriangles = verticesAndTrianglesInRelationToViewFrustum.matchingTriangles;
		var matchingVertices = verticesAndTrianglesInRelationToViewFrustum.matchingVerticesIndices;
		var partiallyMatchingTriangles = verticesAndTrianglesInRelationToViewFrustum.partiallyMatchingTriangles;
		
		var newVertices = new List<int>(matchingVertices);
		var newTriangles = new List<int>(matchingTriangles);

		for (var i = 0; i < partiallyMatchingTriangles.Count; i++)
		{
			var triangle = partiallyMatchingTriangles[i];
			
			// ReSharper disable once ConvertIfStatementToSwitchStatement
			if (triangle.matchingVerticesIndices.Count == 1)
			{
				var v1 = triangle.notMatchingVertices[0];
				var v2 = triangle.notMatchingVertices[1];
				
				var verticesOutsideDifferentPlanes = !v1.notMatchingPlanes[0].Equals(v2.notMatchingPlanes[0]);

				var insiderIndex = triangle.matchingVerticesIndices[0];
				var outsider1Index = triangle.notMatchingVertices[0].index;
				var outsider2Index = triangle.notMatchingVertices[1].index;
					
				var insider = meshVertices[insiderIndex];
				var outsider1 = meshVertices[outsider1Index];
				var outsider2 = meshVertices[outsider2Index];
				
				var newPoints = new[]{outsider1, outsider2};

				
				#region 2 Vertices Outside, each outside a different Plane

				if (verticesOutsideDifferentPlanes)
				{
					var plane1 = triangle.notMatchingVertices[0].notMatchingPlanes[0];
					var plane2 = triangle.notMatchingVertices[1].notMatchingPlanes[0];
					var plane3 = new Plane();

					
					var o = GetClockwiseRotation(new[] {insiderIndex, outsider1Index, outsider2Index}, new[]
					{
						meshTriangles[i + insiderIndex],
						meshTriangles[i + outsider1Index],
						meshTriangles[i + outsider2Index]
					});
					var list = meshVertices;
					plane3.Set3Points
					(
						objTransform.TransformPoint(meshVertices[o[0]]),
						objTransform.TransformPoint(meshVertices[o[1]]),
						objTransform.TransformPoint(meshVertices[o[2]])
					);
					
					newPoints[0] = ProjectPointOnPlane(insider, newPoints[0], plane1, objTransform);
					newPoints[1] = ProjectPointOnPlane(insider, newPoints[1], plane2, objTransform);
					
					if (!MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane3, plane2, out var newPoint))
						MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane2, plane3, out newPoint);
					newPoint = objTransform.InverseTransformPoint(newPoint);
			
					foreach (var p in newPoints)
					{
						meshVertices.Add(p);
						matchingVertices.Add(meshVertices.Count - 1);
					}
					
					meshVertices.Add(newPoint);
					matchingVertices.Add(meshVertices.Count - 1);
			
					CreateTrapezoid(ref newTriangles,
						new[] {insiderIndex, outsider1Index, outsider2Index},
						new[] {meshTriangles[i + insiderIndex], meshVertices.Count - 3, meshVertices.Count - 2},
						new[] {meshVertices.Count - 1, meshVertices.Count - 2, meshVertices.Count - 3}
					);
				}
				#endregion
				
				#region 2 Vertices Outside the same Plane
				else
				{
					var outsiders = new[]{outsider1, outsider2};

					for (var p = 0; p < newPoints.Length; p++)
					{
						var planesVertexIsOutsideOf = triangle.notMatchingVertices[p].notMatchingPlanes;
						var planesVertexIsOutsideOfCount = planesVertexIsOutsideOf.Count();
						
						if (planesVertexIsOutsideOfCount > 1)
						{
							var plane1 = planesVertexIsOutsideOf[0];
							var plane2 = planesVertexIsOutsideOf[1];
							var plane3 = new Plane();
							var o = GetClockwiseRotation(new[] {insiderIndex, outsider1Index, outsider2Index}, new[]
							{
								meshTriangles[i + insiderIndex],
								meshTriangles[i + outsider1Index],
								meshTriangles[i + outsider2Index]
							});
							plane3.Set3Points
							(
								objTransform.TransformPoint(meshVertices[o[0]]),
								objTransform.TransformPoint(meshVertices[o[1]]),
								objTransform.TransformPoint(meshVertices[o[2]])
							);

							if (!MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane3, plane2, out outsiders[p]))
								MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane2, plane3, out outsiders[p]);

							outsiders[p] = objTransform.InverseTransformPoint(outsiders[p]);
						}

						else
						{
							var plane = planesVertexIsOutsideOf[0];
							outsiders[p] = ProjectPointOnPlane(insider, outsiders[p], plane, objTransform);
						}
					}


					foreach (var p in outsiders)
					{
						meshVertices.Add(p);
						matchingVertices.Add(meshVertices.Count - 1);
					}


					CreateTriangle(ref newTriangles,
						new[] {insiderIndex, outsider1Index, outsider2Index},
						new[] {meshTriangles[i + insiderIndex], meshVertices.Count - 2, meshVertices.Count - 1});
				}
				#endregion
			}
			else if (triangle.matchingVerticesIndices.Count == 2)
			{
				var outsiderIndex = triangle.notMatchingVertices[0].index;
				var insiderIndex = matchingVertices[0];
				var insiderIndex2 = matchingVertices[1];
				var outsider = meshVertices[meshTriangles[i + outsiderIndex]];
				var insider = meshVertices[meshTriangles[i + insiderIndex]];
				var insider2 = meshVertices[meshTriangles[i + insiderIndex2]];

				var planesVertexIsOutsideOf = triangle.notMatchingVertices[0].notMatchingPlanes;
				var planesVertexIsOutsideOfCount = planesVertexIsOutsideOf.Count;

				if (planesVertexIsOutsideOfCount == 1)
				{
					var insiders = new[] {insider, insider2};
					var newPoints = new[] {outsider, outsider};

					foreach (var plane in planesVertexIsOutsideOf)
					{
						for (var p = 0; p < newPoints.Length; p++)
						{
							newPoints[p] = ProjectPointOnPlane(insiders[p], newPoints[p], plane,
								objTransform);
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
					var plane1 = planesVertexIsOutsideOf[0];
					var plane2 = planesVertexIsOutsideOf[1];
					var plane3 = new Plane();
					var o = GetClockwiseRotation(new[] {insiderIndex, insiderIndex2, outsiderIndex}, new[]
					{
						meshTriangles[i + insiderIndex],
						meshTriangles[i + insiderIndex2],
						meshTriangles[i + outsiderIndex]
					});
					plane3.Set3Points
					(
						objTransform.TransformPoint(meshVertices[o[0]]),
						objTransform.TransformPoint(meshVertices[o[1]]),
						objTransform.TransformPoint(meshVertices[o[2]])
					);
					
					if (!MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane3, plane2, out var newPoint))
						MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane2, plane3, out newPoint);
					newPoint = objTransform.InverseTransformPoint(newPoint);
					meshVertices.Add(newPoint);
					matchingVertices.Add(meshVertices.Count - 1);
					
					newTriangles.AddRange(new[] {meshTriangles[i + insiderIndex], meshTriangles[i + insiderIndex2], meshVertices.Count - 1});
				}
			}
			else if (triangle.matchingVerticesIndices.Count == 3)
			{
				Debug.LogError("wtf");
			}

		}

		return newVertices;		
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
		var newTriangles = new List<int>();
		for (var i = 0; i < meshTriangles.Length; i += 3)
		{
			var contain = new List<(bool inside, Plane[] planesVertexIsOutsideOf )> {(false, null), (false, null), (false, null)};

			for (var j = 0; j < 3; j++)
			{
				var mV = meshVertices;
				var mT = meshTriangles;
				var planesVertexIsOutsideOf =
					planes.Where(x => !x.GetSide(gameObjectTransform.TransformPoint(mV[mT[i + j]]))).ToArray();
				contain[j] = (matchingVertices.Contains(meshTriangles[i + j]), planesVertexIsOutsideOf);
			}

			var verticesInsideFrustumCount = contain.Count(x => x.inside);
			
			// ReSharper disable once ConvertIfStatementToSwitchStatement
			if (verticesInsideFrustumCount == 3)
			{
				newTriangles.AddRange(new[] {meshTriangles[i], meshTriangles[i + 1], meshTriangles[i + 2]});
			}
			else if (verticesInsideFrustumCount == 2)
			{
				var outsiderIndex = contain.FindIndex(x => !x.inside);
				var insiderIndex = contain.FindIndex(x => x.inside);
				var insiderIndex2 = contain.FindLastIndex(x => x.inside);
				var outsider = meshVertices[meshTriangles[i + outsiderIndex]];
				var insider = meshVertices[meshTriangles[i + insiderIndex]];
				var insider2 = meshVertices[meshTriangles[i + insiderIndex2]];
	
				{
					var p = planes.Select(x => x.GetSide(outsider));
					var c = (planes.Count(x => x.GetSide(outsider)), planes.Count(x => x.GetSide(insider)), planes.Count(x => x.GetSide(insider2)));
					var s = string.Join(", ", p);
				}

				var planesVertexIsOutsideOf = contain[outsiderIndex].planesVertexIsOutsideOf;

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
					var plane1 = contain[outsiderIndex].planesVertexIsOutsideOf[0];
					var plane2 = contain[outsiderIndex].planesVertexIsOutsideOf[1];
					var plane3 = new Plane();
					var o = GetClockwiseRotation(new[] {insiderIndex, insiderIndex2, outsiderIndex}, new[]
					{
						meshTriangles[i + insiderIndex],
						meshTriangles[i + insiderIndex2],
						meshTriangles[i + outsiderIndex]
					});
					plane3.Set3Points
					(
						gameObjectTransform.TransformPoint(meshVertices[o[0]]),
						gameObjectTransform.TransformPoint(meshVertices[o[1]]),
						gameObjectTransform.TransformPoint(meshVertices[o[2]])
					);
					
					if (!MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane3, plane2, out var newPoint))
						MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane2, plane3, out newPoint);
					newPoint = gameObjectTransform.InverseTransformPoint(newPoint);
					meshVertices.Add(newPoint);
					matchingVertices.Add(meshVertices.Count - 1);
					
					newTriangles.AddRange(new[] {meshTriangles[i + insiderIndex], meshTriangles[i + insiderIndex2], meshVertices.Count - 1});
				}
			}
			else if (verticesInsideFrustumCount == 1)
			{
				var insiderIndex = contain.FindIndex(x => x.inside);
				var outsiderIndex = contain.FindIndex(x => !x.inside);
				var outsiderIndex2 = contain.FindLastIndex(x => !x.inside);

				var insider = meshVertices[meshTriangles[i + insiderIndex]];
				var outsider = meshVertices[meshTriangles[i + outsiderIndex]];
				var outsider2 = meshVertices[meshTriangles[i + outsiderIndex2]];

				var outsidersIndexes = new[] {outsiderIndex, outsiderIndex2};

				var outsiders = new[] {outsider, outsider2};

				var verticesOutsideDifferentPlanes = (
					contain[outsiderIndex].planesVertexIsOutsideOf.Length == 1 &&
					contain[outsiderIndex2].planesVertexIsOutsideOf.Length == 1 &&
					!contain[outsiderIndex].planesVertexIsOutsideOf[0]
						.Equals(contain[outsiderIndex2].planesVertexIsOutsideOf[0]));
				
				if (verticesOutsideDifferentPlanes)
				{
					var plane1 = contain[outsiderIndex].planesVertexIsOutsideOf[0];
					var plane2 = contain[outsiderIndex2].planesVertexIsOutsideOf[0];
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
					
					outsiders[0] = ProjectPointOnPlane(insider, outsiders[0], plane1, gameObjectTransform);
					outsiders[1] = ProjectPointOnPlane(insider, outsiders[1], plane2, gameObjectTransform);
					
					if (!MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane3, plane2, out var newPoint))
						MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane2, plane3, out newPoint);
					newPoint = gameObjectTransform.InverseTransformPoint(newPoint);
			
					foreach (var p in outsiders)
					{
						meshVertices.Add(p);
						matchingVertices.Add(meshVertices.Count - 1);
					}
					
					meshVertices.Add(newPoint);
					matchingVertices.Add(meshVertices.Count - 1);
			
					CreateTrapezoid(ref newTriangles,
						new[] {insiderIndex, outsiderIndex, outsiderIndex2},
						new[] {meshTriangles[i + insiderIndex], meshVertices.Count - 3, meshVertices.Count - 2},
						new[] {meshVertices.Count - 1, meshVertices.Count - 2, meshVertices.Count - 3}
						);
				}
				else
				{
					for (var p = 0; p < outsiders.Length; p++)
					{
						var planesVertexIsOutsideOf = contain[outsidersIndexes[p]].planesVertexIsOutsideOf.ToList();
						//planes.Where(x => !x.GetSide(gameObjectTransform.TransformPoint(outsiders[p]))).ToList();

						var planesVertexIsOutsideOfCount = planesVertexIsOutsideOf.Count();
						
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

							if (!MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane3, plane2, out outsiders[p]))
								MathExtensions.PlanesIntersectAtSinglePoint(plane1, plane2, plane3, out outsiders[p]);

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
		if (indices[0] < indices[1])
			return indices[1] < indices[2] ? new[] {triangles[0], triangles[1], triangles[2]} : new[] {triangles[0], triangles[2], triangles[1]};
		
		return indices[2] > indices[0] ? new[] {triangles[1], triangles[0], triangles[2]} : new[] {triangles[1], triangles[2], triangles[0]};
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
