using UnityEngine;
using UnityEditor;
using System.Collections;

[ExecuteInEditMode]
public class SunMover : MonoBehaviour 
{
	public bool updateInEditor;
	[Range(0,12)]
	public float monthOfYear;
	[Range(0,24)]
	public float timeOfDay;
	public float speed;
	public Color nightSkyColor;
	public Color daySkyColor;

	// Use this for initialization
	void Start () 
	{
	
	}
	
	// Update is called once per frame
	void Update () 
	{
		if( !updateInEditor && !Application.isPlaying )
		{
			return;
		}
		timeOfDay += speed * Time.deltaTime;
		//dayOfYear += speed * Time.deltaTime;

		float lattitude = 52.0f;
		float wobble = 23.5f;

		Vector3 rotAxis = Vector3.forward;
		// Move off axis;
		rotAxis = Quaternion.AngleAxis( lattitude + wobble*Mathf.Sin( ((monthOfYear/12f)+0.25f) * 2.0f*Mathf.PI), Vector3.left ) * rotAxis;

		Vector3 dir = Quaternion.AngleAxis( 90.0f, Vector3.left ) * rotAxis;
		dir = Quaternion.AngleAxis( 360.0f * (timeOfDay/24f), rotAxis ) * dir;

		/*if( dir.y > 0.0f )
		{
			dir = -1.0f*dir; // Always day!
		}*/

		transform.rotation = Quaternion.AngleAxis( -90.0f, Vector3.up ) * Quaternion.LookRotation( dir );

		//rotAxis = Quaternion.AngleAxis( 360.0f*dayOfYear, Vector3.up ) * rotAxis;

		float crossOver = 0.1f;
		Camera.main.backgroundColor = Color.Lerp( daySkyColor, nightSkyColor, dir.y / crossOver );
		if( dir.y > 0.0f )
		{
			this.GetComponent<Light>().intensity = 0.0f;
		}
		else
		{
			this.GetComponent<Light>().intensity = 1.0f;
		}
	}
}
