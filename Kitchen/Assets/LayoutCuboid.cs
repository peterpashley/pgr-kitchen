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
	public WatchedVector3 edgeMin;
	public WatchedVector3 edgeMax;

	// Use this for initialization
	void Start () 
	{
	}
	
	// Update is called once per frame
	void Update () 
	{
		if(    edgeMin.Update() 
			|| edgeMax.Update() )
		{
			UpdateMesh();
		}
	}

	void UpdateMesh()
	{
		Vector3 size = edgeMax.value - edgeMin.value;
		this.transform.localScale = size;
		this.transform.position = 0.5f*(edgeMax.value + edgeMin.value);
	}
}
