using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
public class OptionSelector : MonoBehaviour 
{
	public KeyCode cycleOptionKey;
	public TextMesh optionText;

	[Range(0,20)]
	public int option;

	public List<Transform> options = new List<Transform>();

	// Use this for initialization
	void Start () 
	{
		FindOptions();
		SetOption( option );
	}

	void SetOption( int value )
	{
		option = value;

		if( optionText )
		{
			optionText.text = options[option].name;
		}
		
		UpdateOptions();
	}
	
	// Update is called once per frame
	void Update () 
	{
		if( !Application.isPlaying )
		{
			FindOptions();
		}

		if( cycleOptionKey != KeyCode.None && Input.GetKeyDown(cycleOptionKey) )
		{
			SetOption( (option+1)%options.Count );
		}

		UpdateOptions();
	}

	void FindOptions()
	{
		options.Clear();
		
		for( int i=0 ; i<transform.childCount ; i++ )
		{
			options.Add( transform.GetChild(i) );
		}
		
		options.Sort( delegate(Transform x, Transform y) {
			return x.name.CompareTo(y.name);
		} );
	}

	void UpdateOptions () 
	{
		for( int i=0 ; i<options.Count ; i++ )
		{
			if( options[i].gameObject.activeSelf != (i==option) )
			{
				options[i].gameObject.SetActive( i==option);
			}
		}
	}
}
