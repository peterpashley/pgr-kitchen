using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;

public class Measurement : MonoBehaviour 
{
	public enum MeasureMode
	{
		To,
		Between,
	}
	public MeasureMode mode = MeasureMode.Between;
	public Color color = Color.black;
	public float distance;

	void OnDrawGizmos()
	{
		RaycastHit hitInfo;

		Gizmos.color = color;
		Gizmos.DrawCube( transform.position, 0.01f*Vector3.one);

		if( Physics.Raycast( this.transform.position, this.transform.forward, out hitInfo ) )
		{
			distance = hitInfo.distance;
			Gizmos.DrawLine( this.transform.position, hitInfo.point );
			Gizmos.DrawRay( hitInfo.point, 0.05f*(-this.transform.up-this.transform.forward) );

			if( mode == MeasureMode.Between )
			{
				if( Physics.Raycast( this.transform.position, -this.transform.forward, out hitInfo ) )
				{
					Gizmos.DrawLine( this.transform.position, hitInfo.point );
					Gizmos.DrawRay( hitInfo.point, 0.05f*(-this.transform.up+this.transform.forward) );

					distance += hitInfo.distance;

					distance = Mathf.Round( 1000f*distance );

					EditorText.DrawText( transform.position - 0.01f*transform.up, "" + (distance) + "mm", color );
				}
			}
			else
			{
				Gizmos.DrawRay( transform.position, 0.05f*(-this.transform.up+this.transform.forward) );

				distance = Mathf.Round( 1000f*distance );

				EditorText.DrawText( 0.5f*(transform.position+hitInfo.point) - 0.01f*transform.up, "" + (distance) + "mm", color );
			}
		}
		
	}
}

public class EditorText
{
	public static void DrawText( Vector3 position, string text, Color color )
	{
		GUIStyle style = new GUIStyle();
		style.normal.textColor = color;
		Handles.Label( position, text, style );
	}
		
}
