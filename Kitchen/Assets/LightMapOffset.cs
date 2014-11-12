using UnityEngine;
using System.Collections;
using System.Collections.Generic;


// Moves apart (and restores) a list of game objects in order for them not to shade each other during AO generation.
public class LightMapOffset : MonoBehaviour 
{
	
	public float spacing = 100.0f;			//default spacing
	public List<GameObject> gameObjectsArray = new List<GameObject>();
	public List<Vector3> originalPositions = new List<Vector3>();
	private bool moveButtonPressed = false;

	//[EditorButton("RestoreGameObjectPositions")]
	public int restorePositionsButton;
	
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
	
	public void MoveGameObjects()
	{
		//stop calling this method more than once
		Debug.Log("MoveGameObjects " + moveButtonPressed + " " + gameObjectsArray.Count);
		
		if(!moveButtonPressed)
		{
			moveButtonPressed = true;
			if( gameObjectsArray.Count>0 )
			{
				originalPositions.Clear();
				
				Debug.Log("Copy original position " + spacing + " array size " + gameObjectsArray.Count);
				for( int i=0;i<gameObjectsArray.Count;i++ )
				{
					GameObject gameObject = gameObjectsArray[i];
					originalPositions.Add(gameObject.transform.position);
				}
				
				for( int i=0;i<gameObjectsArray.Count;i++ )
				{
					GameObject gameObject = gameObjectsArray[i];
					Vector3 position = originalPositions[i];
					position.Set( position.x, position.y + ((i+1)*spacing), position.z );
					gameObject.transform.position = position;
				}
			}
		}
	}
	
	public void RestoreGameObjectPositions()
	{
		Debug.Log("RestoreGameObjectPositions " + moveButtonPressed);
		if(gameObjectsArray.Count>0 && originalPositions.Count>0)
		{
			//restore gameObject original position
			for( int i=0;i<gameObjectsArray.Count;i++ )
			{
				GameObject gameObject = gameObjectsArray[i];
				gameObject.transform.position = originalPositions[i];
				
			}
		}
		
		moveButtonPressed = false;
		
	}
	
	
	
	
}
