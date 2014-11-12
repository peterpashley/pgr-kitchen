﻿using UnityEngine;
using System.Collections;

public class VRPlayerController : MonoBehaviour 
{
	public enum Mode
	{
		Normal,
		VR,
	}
	public Mode mode;
	public Camera normalCamera;
	public OVRCameraController vrController;
	public float speed;
	public OptionSelector selector;

	float _moveTimer;
	CharacterController charController;

	// Use this for initialization
	void Start () 
	{
		normalCamera.enabled = mode == Mode.Normal;
		vrController.gameObject.SetActive( mode == Mode.VR );

		foreach( MouseLook ml in GameObject.FindObjectsOfType<MouseLook>() )
		{
			ml.enabled = (mode == Mode.Normal);
		}

		charController = this.GetComponent<CharacterController>();
	}
	
	// Update is called once per frame
	void Update ()
	{
		if( Input.GetMouseButtonUp(0) && _moveTimer < 0.5f )
		{
			selector.SetOption( (selector.option+1)%selector.options.Count );
		}

		if( Input.GetMouseButton(0) )
		{
			_moveTimer += Time.deltaTime;
			
			if( _moveTimer > 0.5f )
			{
				Vector3 fwd = Vector3.forward;
				if( mode == Mode.Normal )
				{
					fwd = this.transform.forward;
				}
				else
				{
					vrController.GetCameraForward( ref fwd );
				}
				
				charController.Move( fwd * speed * Time.deltaTime );
			}
		}
		else
		{
			_moveTimer = 0.0f;
		}
	}
}