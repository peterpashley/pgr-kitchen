using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class PathRuler : MonoBehaviour 
{
	public float totalLength;

	// Use this for initialization
	void Start () 
	{
	
	}
	
	// Update is called once per frame
	void Update () 
	{
		totalLength = 0f;
		var rulers = GetComponentsInChildren<Measurement>();
		for( int i=0 ; i<rulers.Length-1 ; i++ )
		{
			rulers[i].distance = Mathf.Round((rulers[i].transform.position - rulers[i+1].transform.position).magnitude*1000f);
			rulers[i].transform.LookAt(rulers[i+1].transform);

			totalLength += rulers[i].distance;
		}

		rulers[rulers.Length-1].distance = 0;
	}

	void OnDrawGizmos()
	{
		Vector3 max = Vector3.zero;
		Vector3 min = Vector3.zero;

		var rulers = GetComponentsInChildren<Measurement>();
		for( int i=0 ; i<rulers.Length-1 ; i++ )
		{
			for( int j=0 ; j<3 ; j++ )
			{
				max[j] = Mathf.Max( max[j], rulers[i].transform.localPosition[j] );
				min[j] = Mathf.Min( min[j], rulers[i].transform.localPosition[j] );
			}
		}

		Vector3 pos = transform.TransformPoint( 0.5f*(max + min) );

		EditorText.DrawText( pos, "Length=" + (totalLength) + "mm", Color.blue );
	}
}
