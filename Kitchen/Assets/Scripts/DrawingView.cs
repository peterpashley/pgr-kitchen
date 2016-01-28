using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class DrawingView : MonoBehaviour 
{
	public bool updateView;
	public MeshRenderer linePrefab;
	public Material blackLine;
	public Material greyLine;

	// Use this for initialization
	void Start () 
	{
	
	}
	
	// Update is called once per frame
	void Update () 
	{
		if( updateView )
		{
			updateView = false;
			Camera cam = this.GetComponent<Camera>();
			cam.aspect = 1f;

			float pixelSize = 2f*cam.orthographicSize / (float)Screen.width;

			while( transform.childCount > 0 )
			{
				Transform child = transform.GetChild(0);
				child.parent = null;
				GameObject.DestroyImmediate( child.gameObject );
			}

			Quaternion baseRotation = linePrefab.transform.rotation;

			Vector3[] offsetMultipliers = new Vector3[4];
			offsetMultipliers[0] = new Vector3(1,0,0);
			offsetMultipliers[1] = new Vector3(0,1,0);
			offsetMultipliers[2] = new Vector3(-1,0,0);
			offsetMultipliers[3] = new Vector3(0,-1,0);

			var cubes = GameObject.FindObjectsOfType<LayoutCuboid>();
			foreach( var cube in cubes )
			{
				float lineWidth = pixelSize * (float)cube.edgeLineSize;

				Vector3 eyeSize = cam.transform.InverseTransformDirection( cube.transform.TransformDirection( cube.transform.localScale ) ); 
				eyeSize.x = Mathf.Abs( eyeSize.x );
				eyeSize.y = Mathf.Abs( eyeSize.y );
				eyeSize.z = Mathf.Abs( eyeSize.z );
				Vector3 eyePos = cam.transform.InverseTransformPoint( cube.transform.position );

				if( Mathf.Abs(eyePos.x)-0.5f*eyeSize.x > cam.orthographicSize || Mathf.Abs(eyePos.y)-0.5f*eyeSize.y > cam.orthographicSize )
				{
					continue;
				}

				for( int i=0 ; i<4 ; i++ )
				{
					var renderer = GameObject.Instantiate(linePrefab);
					Vector3 offset = Vector3.zero;
					if( cube.edgeLineStrength < 0.75f )
					{
						renderer.sharedMaterial = greyLine;
					}
					else
					{
						renderer.sharedMaterial = blackLine;
					}

					offset.x = offsetMultipliers[i].x * eyeSize.x;
					offset.y = offsetMultipliers[i].y * eyeSize.y;
					offset.z = offsetMultipliers[i].z * eyeSize.z;

					offset *= 0.5f;
					float angle = 90f * i;
					Vector3 pos = cam.transform.TransformPoint(eyePos + offset);

					// Align to pixels:
					Vector3 posScreen = cam.WorldToScreenPoint( pos );
					posScreen.x = Mathf.Round(posScreen.x);
					posScreen.y = Mathf.Round(posScreen.y);
					pos = cam.ScreenToWorldPoint( posScreen );
					//pos.x = lineWidth*Mathf.Round(pos.x/lineWidth+0.5f);
					//pos.z = lineWidth*Mathf.Round(pos.z/lineWidth+0.5f);
					renderer.transform.position = pos;
					renderer.transform.rotation = cam.transform.rotation * Quaternion.AngleAxis( angle, Vector3.forward );
					renderer.transform.parent = this.transform;
					Vector3 scale = renderer.transform.localScale;
					scale.x = lineWidth;
					scale.y = 2f*Mathf.Max(0.5f*eyeSize.x - Mathf.Abs(offset.x), 0.5f*eyeSize.y - Mathf.Abs(offset.y) );
					renderer.transform.localScale = scale;
				}
			}
		}
	}
}
