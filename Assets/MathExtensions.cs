using UnityEngine;

public static class MathExtensions
{
	
	public static bool PlanesIntersectAtSinglePoint( Plane p0, Plane p1, Plane p2, out Vector3 intersectionPoint )
	{
		const float EPSILON = 1e-4f;

		var det = Vector3.Dot( Vector3.Cross( p0.normal, p1.normal ), p2.normal );
		if( det < EPSILON )
		{
			intersectionPoint = Vector3.zero;
			return false;
		}

		intersectionPoint = 
			( -( p0.distance * Vector3.Cross( p1.normal, p2.normal ) ) -
			  ( p1.distance * Vector3.Cross( p2.normal, p0.normal ) ) -
			  ( p2.distance * Vector3.Cross( p0.normal, p1.normal ) ) ) / det;

		return true;
	}
	
	public static bool IntersectionWithPlane(this Plane plane, Plane otherPlane, out Vector3 linePoint, out Vector3 lineVec)
	{
		Debug.DrawLine(plane.ClosestPointOnPlane(Vector3.zero), otherPlane.ClosestPointOnPlane(Vector3.zero), Color.yellow, 1111111f);
		return PlanePlaneIntersection (out linePoint, out lineVec,
							plane.normal, plane.ClosestPointOnPlane(Vector3.zero),
							otherPlane.normal, otherPlane.ClosestPointOnPlane(Vector3.zero));
	}
	//Find the line of intersection between two planes.	The planes are defined by a normal and a point on that plane.
	//The outputs are a point on the line and a vector which indicates it's direction. If the planes are not parallel, 
	//the function outputs true, otherwise false.
	public static bool PlanePlaneIntersection(out Vector3 linePoint, out Vector3 lineVec, Vector3 plane1Normal, Vector3 plane1Position, Vector3 plane2Normal, Vector3 plane2Position){
 
		linePoint = Vector3.zero;
		lineVec = Vector3.zero;
 
		//We can get the direction of the line of intersection of the two planes by calculating the 
		//cross product of the normals of the two planes. Note that this is just a direction and the line
		//is not fixed in space yet. We need a point for that to go with the line vector.
		lineVec = Vector3.Cross(plane1Normal, plane2Normal);
 
		//Next is to calculate a point on the line to fix it's position in space. This is done by finding a vector from
		//the plane2 location, moving parallel to it's plane, and intersecting plane1. To prevent rounding
		//errors, this vector also has to be perpendicular to lineDirection. To get this vector, calculate
		//the cross product of the normal of plane2 and the lineDirection.		
		Vector3 ldir = Vector3.Cross(plane2Normal, lineVec);		
 
		float denominator = Vector3.Dot(plane1Normal, ldir);
 
		//Prevent divide by zero and rounding errors by requiring about 5 degrees angle between the planes.
		if(Mathf.Abs(denominator) > 0.006f){
 
			Vector3 plane1ToPlane2 = plane1Position - plane2Position;
			float t = Vector3.Dot(plane1Normal, plane1ToPlane2) / denominator;
			linePoint = plane2Position + t * ldir;
 
			return true;
		}
 
		//output not valid
		else{
			return false;
		}
	}
}