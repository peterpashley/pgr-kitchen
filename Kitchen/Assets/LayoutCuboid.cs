using UnityEngine;
using System.Collections;

[System.Serializable]
public class WatchedVector3
{
	public Vector3 value;
	private Vector3 _value;

	public void Start()
	{
		_value = value;
	}

	public bool Update()
	{
		if( _value != value )
		{
			_value = value;
			return true;
		}
		return false;
	}
}

[ExecuteInEditMode]
public class LayoutCuboid : MonoBehaviour 
{
	public enum VerticalPivot
	{
		Centre,
		Base,
	}

	public VerticalPivot verticalPivot = VerticalPivot.Centre;

	public WatchedVector3 edgeMin;
	public WatchedVector3 edgeMax;
	public Vector3 size;

	Vector3 _lastPosition;
	Quaternion _lastRotation;

	// Use this for initialization
	void Start () 
	{
		_lastPosition = transform.localPosition;
		_lastRotation = transform.rotation;
	}
	
	// Update is called once per frame
	void Update () 
	{
		Vector3 nowPos = transform.localPosition;
		if( _lastPosition != nowPos )
		{
			Vector3 delta = nowPos - _lastPosition;
			edgeMax.value += delta;
			edgeMin.value += delta;

			_lastPosition = nowPos;
		}

		if(    edgeMin.Update() 
			|| edgeMax.Update() 
			|| _lastRotation != transform.rotation )
		{
			UpdateMesh();
		}
	}

	void UpdateMesh()
	{
		size = edgeMax.value - edgeMin.value;
		Vector3 scale = size;
		float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(this.transform.localRotation.eulerAngles.y, 0));
		if( 45f < deltaAngle && deltaAngle < 135f)
		{
			float temp = scale.x;
			scale.x = scale.z;
			scale.z = temp;
		}

		this.transform.localScale = scale;
		Vector3 pos = 0.5f*(edgeMax.value + edgeMin.value);
		if( verticalPivot == VerticalPivot.Base )
		{
			pos.y = edgeMin.value.y;
		}
		this.transform.localPosition = pos;
		_lastPosition = this.transform.localPosition;
		_lastRotation = this.transform.rotation;
	}
}
