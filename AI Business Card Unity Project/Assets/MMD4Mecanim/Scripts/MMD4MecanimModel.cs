//#define _SHADER_TEST

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using MorphCategory		= MMD4MecanimData.MorphCategory;
using MorphType			= MMD4MecanimData.MorphType;
using MorphData			= MMD4MecanimData.MorphData;
using MorphMotionData	= MMD4MecanimData.MorphMotionData;
using BoneData			= MMD4MecanimData.BoneData;

using Bone				= MMD4MecanimBone;

[ExecuteInEditMode ()] // for Morph
public partial class MMD4MecanimModel : MonoBehaviour
{
	public enum PhysicsEngine
	{
		None,
		BulletPhysics,
	}
	
	public class Morph
	{
		public float							weight;
		public float							weight2;

		public float							_animWeight;
		public float							_appendWeight;
		public float							_updateWeight;
		public float							_updatedWeight;

		public MorphData						morphData;
		
		public MorphType morphType {
			get {
				if( morphData != null ) {
					return morphData.morphType;
				}
				return MorphType.Group;
			}
		}

		public MorphCategory morphCategory {
			get {
				if( morphData != null ) {
					return morphData.morphCategory;
				}
				return MorphCategory.Base;
			}
		}

		public string name {
			get {
				if( morphData != null ) {
					return morphData.nameJp;
				}
				return null;
			}
		}
	}
	
	[System.Serializable]
	public class Anim
	{
		public TextAsset						animFile;
		public string							animatorStateName;
		public AudioClip						audioClip;
		
		[NonSerialized]
		public MMD4MecanimData.AnimData			animData;
		[NonSerialized]
		public int								animatorStateNameHash;
		
		public struct MorphMotion
		{
			public Morph						morph;
			public int							lastKeyFrameIndex;
		}
		
		[NonSerialized]
		public MorphMotion[]					morphMotionList;
	}

	[System.Serializable]
	public class BulletPhysics
	{
		public bool joinLocalWorld = true;
		public bool useOriginalScale = true;
		public bool useCustomResetTime = false;
		public float resetMorphTime = 1.8f;
		public float resetWaitTime = 1.2f;
		public MMD4MecanimBulletPhysics.WorldProperty worldProperty;
		public MMD4MecanimBulletPhysics.MMDModelRigidBodyProperty mmdModelRigidBodyProperty;
	}
	
	private class CloneMesh
	{
		public Mesh									mesh;
		public Vector3[]							vertices;
		public Vector3[]							backupVertices;
		public bool									updatedVertices;
	}

	public class CloneMaterial
	{
		public Material[]							materials;
		public MMD4MecanimData.MorphMaterialData[]	materialData;
		public MMD4MecanimData.MorphMaterialData[]	backupMaterialData;
		public bool[]								updateMaterialData;
	}

	private struct MorphBlendShape
	{
		// Key ... morphID Value ... blendShapeIndex
		public int[]								blendShapeIndices;
	}

	public bool										initializeOnAwake = false;
	public bool										postfixRenderQueue = true;
	public bool										updateWhenOffscreen = true;
	public bool										animEnabled = true;
	public bool										animSyncToAudio = true;
	public TextAsset								modelFile;
	public TextAsset								indexFile;
	public AudioSource								audioSource;

	public bool boneInherenceEnabled				= true;
	public bool boneInherenceEnabledGeneric			= false;

	public bool pphEnabled							= true;
	public bool pphEnabledNoAnimation				= true;

	public bool										pphShoulderEnabled = true;
	public float									pphShoulderFixRate = 0.7f;

	public bool										ikEnabled = false;
	bool											_boneInherenceEnabledCached;
	bool											_ikEnabledCached;

	public enum PPHType
	{
		Shoulder,
	}

	public class PPHBone
	{
		public PPHType			pphType;
		public GameObject		target;
		public List<GameObject>	childSkeletons;
		public Quaternion[]		childRotations;

		public PPHBone( PPHType pphType, GameObject target )
		{
			this.pphType = pphType;
			this.target = target;
		}

		public void AddChildSkeleton( GameObject childSkeleton )
		{
			if( this.childSkeletons == null ) {
				this.childSkeletons = new List<GameObject>();
			}
			this.childSkeletons.Add( childSkeleton );
		}

		public void SnapshotChildRotations()
		{
			if( this.childSkeletons != null ) {
				if( this.childRotations == null || this.childRotations.Length != this.childSkeletons.Count ) {
					this.childRotations = new Quaternion[this.childSkeletons.Count];
				}
				for(int i = 0; i < this.childSkeletons.Count; ++i) {
					this.childRotations[i] = this.childSkeletons[i].transform.rotation;
				}
			}
		}

		public void RestoreChildRotations()
		{
			if( this.childSkeletons != null && this.childRotations != null && this.childSkeletons.Count == this.childRotations.Length ) {
				for(int i = 0; i < this.childSkeletons.Count; ++i) {
					this.childSkeletons[i].transform.rotation = this.childRotations[i];
				}
			}
		}
	}

	private List<PPHBone>							_pphBones = new List<PPHBone>();

	public MMD4MecanimData.ModelData modelData {
		get { return _modelData; }
	}
	
	public byte[] modelFileBytes {
		get { return (modelFile != null) ? modelFile.bytes : null; }
	}
	
	[NonSerialized]
	public Bone[]									boneList;
	[NonSerialized]
	public IK[]										ikList;
	[NonSerialized]
	public Morph[]									morphList;
	public Anim[]									animList;
	public PhysicsEngine							physicsEngine;
	public BulletPhysics							bulletPhysics;
	
	private bool									_initialized;
	private Bone									_rootBone;
	private Bone[]									_sortedBoneList;
	private MeshRenderer[]							_meshRenderers;
	private SkinnedMeshRenderer[]					_skinnedMeshRenderers;
	private CloneMesh[]								_cloneMeshes;
	private MorphBlendShape[]						_morphBlendShapes;
	private CloneMaterial[]							_cloneMaterials;
	private bool									_supportDeferred;
	private Light									_deferredLight;
	private HashSet<GameObject>						_humanoidBones = new HashSet<GameObject>();

	public MMD4MecanimData.ModelData				_modelData;
	public MMD4MecanimData.IndexData				_indexData;

	public bool isSkinning {
		get {
			return _skinnedMeshRenderers != null && _skinnedMeshRenderers.Length > 0;
		}
	}

	// for Inspector.
	public enum EditorViewPage {
		Model,
		Bone,
		IK,
		Morph,
		Anim,
		Physics,
	}
	
	[HideInInspector]
	public EditorViewPage							editorViewPage;
	[HideInInspector]
	public byte										editorViewMorphBits = 0x0f;
	[NonSerialized]
	public Mesh										defaultMesh;
	
	private Animator								_animator;
	private MMD4MecanimBulletPhysics.MMDModel		_bulletPhysicsMMDModel;
	private MMD4MecanimModel.Anim					_currentAnim;
	private MMD4MecanimModel.Anim					_playingAudioAnim;
	private float									_prevDeltaTime;
	private float[]									_animMorphCategoryWeights;

	public System.Action							onUpdating;
	public System.Action							onUpdated;
	public System.Action							onLateUpdating;
	public System.Action							onLateUpdated;

	public Bone GetRootBone()
	{
		return _rootBone;
	}

	public Bone GetBone( int boneID )
	{
		if( boneID >= 0 && boneID < this.boneList.Length ) {
			return this.boneList[boneID];
		}
		return null;
	}

	public Morph GetMorph( string morphName )
	{
		return GetMorph( morphName, false );
	}

	public Morph GetMorph( string morphName, bool isStartsWith )
	{
		if( this.modelData != null ) {
			int morphIndex = this.modelData.GetMorphDataIndex( morphName, isStartsWith );
			if( morphIndex != -1 ) {
				return this.morphList[morphIndex];
			}
		}
		
		return null;
	}

	void Awake()
	{
		if( initializeOnAwake ) {
			Initialize();
		}
	}
	
	void Start()
	{
		Initialize();
	}

	void Update()
	{
		if( !Application.isPlaying ) {
			return;
		}

		if( this.onUpdating != null ) {
			this.onUpdating();
		}

		if( _prevDeltaTime == 0.0f ) { // for _UpdateAnim()
			_prevDeltaTime = Time.deltaTime;
		}
		
		_UpdateAnim();
		_UpdateAnim2();
		_UpdateMorph();

		_prevDeltaTime = Time.deltaTime;

		_UpdateBone();

		if( this.onUpdated != null ) {
			this.onUpdated();
		}
	}

	void LateUpdate()
	{
		if( !Application.isPlaying ) {
			return;
		}

		if( this.onLateUpdating != null ) {
			this.onLateUpdating();
		}

		_LateUpdateBone();

		if( this.onLateUpdated != null ) {
			this.onLateUpdated();
		}
	}

	public void ForceUpdateMorph()
	{
		_UpdateMorph();
	}

	public void ForceUpdatePPHBones()
	{
		_UpdatePPHBones();
	}

	void OnRenderObject()
	{
		//Debug.Log( Camera.current.projectionMatrix );
		//Matrix4x4 mat = Camera.current.projectionMatrix;
		//Debug.Log ( mat );
		//float rn = (-mat.m32 - mat.m22) / mat.m23;
		//float scale = rn / mat.m11;
		//Debug.Log( "znear:" + (1.0f / rn) + " rn:" + rn + " edge_scale:" + scale );

		_UpdatedDeffered();
	}

	void _UpdatedDeffered()
	{
#if _SHADER_TEST
#else
		if( !_supportDeferred ) {
			return;
		}
#endif
		{
			_deferredLight = null;
			Light[] lights = FindObjectsOfType( typeof(Light) ) as Light[];
			if( lights != null ) {
				foreach( Light light in lights ) {
					if( light.type == LightType.Directional && light.enabled && light.gameObject.activeSelf ) {
						if( _deferredLight == null || _deferredLight.intensity < light.intensity ) {
							_deferredLight = light;
						}
						break;
					}
				}
			}
		}

		_SetDeferredShaderSettings( _deferredLight );
	}

	void _SetDeferredShaderSettings( Light directionalLight )
	{
		float defLightAtten = 0.0f;
		Color defLightColor0 = Color.white;
		Vector4 defLightDir = new Vector4( 0.0f, 0.0f, 1.0f, 1.0f );
		if( directionalLight != null ) {
			defLightAtten = directionalLight.intensity;
			defLightColor0 = directionalLight.color;
			Matrix4x4 lightMat = directionalLight.gameObject.transform.localToWorldMatrix;
			defLightDir.x = -lightMat.m02;
			defLightDir.y = -lightMat.m12;
			defLightDir.z = -lightMat.m22;
		}
		//Vector4 defViewDir = new Vector4( 0.0f, 0.0f, 1.0f, 1.0f );
		Color backgroundColor = Color.black;
		if( Camera.current != null ) {
			/*
			Matrix4x4 cameraMat = Camera.current.transform.localToWorldMatrix;
			defViewDir.x = -cameraMat.m02;
			defViewDir.y = -cameraMat.m12;
			defViewDir.z = -cameraMat.m22;
			*/
			backgroundColor = Camera.current.backgroundColor;
		}

		if( _cloneMaterials != null ) {
			for( int i = 0; i < _cloneMaterials.Length; ++i ) {
				Material[] materials = _cloneMaterials[i].materials;
				if( materials != null ) {
					for( int m = 0; m < materials.Length; ++m ) {
						Material material = _cloneMaterials[i].materials[m];
						if( material != null && MMD4MecanimCommon.IsDeferredShader( material ) ) {
							if( Application.isPlaying || material.GetFloat("_DefLightAtten") != defLightAtten ) {
								material.SetFloat( "_DefLightAtten", defLightAtten );
							}
							if( Application.isPlaying || material.GetColor("_DefLightColor0") != defLightColor0 ) {
								material.SetColor( "_DefLightColor0", defLightColor0 );
							}
							if( Application.isPlaying || material.GetVector("_DefLightDir") != defLightDir ) {
								material.SetVector( "_DefLightDir", defLightDir );
							}
							/*
							if( Application.isPlaying ) {
								material.SetVector( "_DefViewDir", defViewDir );
							}
							*/
							if( Application.isPlaying ) {
								material.SetColor( "_DefClearColor", backgroundColor );
							}
						}
					}
				}
			}
		}
	}

	void OnDestroy()
	{
		if( this.ikList != null ) {
			for( int i = 0; i < this.ikList.Length; ++i ) {
				if( this.ikList[i] != null ) {
					this.ikList[i].Destroy();
				}
			}
			this.ikList = null;
		}

		_sortedBoneList = null;

		if( this.boneList != null ) {
			for( int i = 0; i < this.boneList.Length; ++i ) {
				if( this.boneList[i] != null ) {
					this.boneList[i].Destroy();
				}
			}
			this.boneList = null;
		}

		if( _bulletPhysicsMMDModel != null && !_bulletPhysicsMMDModel.isExpired ) {
			MMD4MecanimBulletPhysics instance = MMD4MecanimBulletPhysics.instance;
			if( instance != null ) {
				instance.DestroyMMDModel( _bulletPhysicsMMDModel );
			}
		}
		_bulletPhysicsMMDModel = null;
	}

	public void Initialize()
	{
		if( !Application.isPlaying ) {
			InitializeOnEditor();
			return;
		}

		if( _initialized ) {
			return;
		}
		
		_initialized = true;
		
		_InitializeMesh();
		_InitializeModel();
		_PrepareBlendShapes();
		_InitializeBlendShapes();
		_InitializeIndex();
		_InitializeCloneMesh();
		_InitializeAnimatoion();
		_InitializePhysicsEngine();
		_InitializePPHBones();
        
	}

	public AudioSource GetAudioSource()
	{
		if( this.audioSource == null ) {
			this.audioSource = this.gameObject.GetComponent< AudioSource >();
			if( this.audioSource == null ) {
				this.audioSource = this.gameObject.AddComponent< AudioSource >();
			}
		}

		return this.audioSource;
	}

	public void InitializeOnEditor()
	{
		if( _modelData == null || _cloneMaterials == null || _cloneMaterials.Length == 0 ) {
			_initialized = false;
		}
		if( _modelData == null && this.modelFile == null ) {
			return;
		}

		if( _modelData == null ) {
			_modelData = MMD4MecanimData.BuildModelData( this.modelFile );
			if( _modelData == null ) {
				Debug.LogError( this.gameObject.name + ":modelFile is unsupported format." );
				return;
			}
		}
		
		if( _modelData != null ) {
			if( _modelData.boneDataList != null ) {
				if( this.boneList == null || this.boneList.Length != _modelData.boneDataList.Length ) {
					_initialized = false;
				}
			}
		}
		
		if( _initialized ) {
			return;
		}
		
		_initialized = true;
		_InitializeMesh();
		_InitializeModel();
		_PrepareBlendShapes();
		//_InitializeIndex();
		_InitializeBlendShapes();
		_InitializeCloneMesh();
		_InitializeAnimatoion();
		//_InitializePhysicsEngine();
		//_InitializePPHBones();
	}

	private void _InitializeMesh()
	{
		if( _meshRenderers == null || _meshRenderers.Length == 0 ) {
			_meshRenderers = MMD4MecanimCommon.GetMeshRenderers( this.gameObject );
		}
		if( _skinnedMeshRenderers == null || _skinnedMeshRenderers.Length == 0 ) {
			_skinnedMeshRenderers = MMD4MecanimCommon.GetSkinnedMeshRenderers( this.gameObject );
			if( _skinnedMeshRenderers != null ) {
				foreach( SkinnedMeshRenderer skinnedMeshRenderer in _skinnedMeshRenderers ) {
					if( skinnedMeshRenderer.updateWhenOffscreen != this.updateWhenOffscreen ) {
						skinnedMeshRenderer.updateWhenOffscreen = this.updateWhenOffscreen;
					}
				}
			}
		}

		if( _skinnedMeshRenderers != null && _skinnedMeshRenderers.Length > 0 ) {
			if( this.defaultMesh == null ) {
				this.defaultMesh = _skinnedMeshRenderers[0].sharedMesh;
			}
		}

		if( _meshRenderers != null && _meshRenderers.Length > 0 ) {
			MeshFilter meshFilter = gameObject.GetComponent< MeshFilter >();
			if( meshFilter != null ) {
				if( this.defaultMesh == null ) {
					this.defaultMesh = meshFilter.sharedMesh;
				}
			}
		}
	}

	private bool _PrepareBlendShapes()
	{
#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2
		// Not supported BlendShapes.
		return false;
#else
		bool blendShapesAnything = false;
		// Reset blendShapes.
		if( _skinnedMeshRenderers != null ) {
			foreach( SkinnedMeshRenderer skinnedMeshRenderer in _skinnedMeshRenderers ) {
				if( skinnedMeshRenderer.sharedMesh != null ) {
					for( int b = 0; b < skinnedMeshRenderer.sharedMesh.blendShapeCount; ++b ) {
						if( Application.isPlaying ) {
							skinnedMeshRenderer.SetBlendShapeWeight( b, 0.0f );
						}
						blendShapesAnything = true;
					}
				}
			}
		}
		return blendShapesAnything;
#endif
	}

	private void _InitializeBlendShapes()
	{
#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2
		// Not supported BlendShapes.
#else
		if( _skinnedMeshRenderers != null && _modelData != null &&
		   _modelData.morphDataList != null && _modelData.morphDataList.Length > 0 ) {
			if( _morphBlendShapes == null || _morphBlendShapes.Length != _skinnedMeshRenderers.Length ) {
				_morphBlendShapes = null;
				bool blendShapeAnything = false;
				foreach( SkinnedMeshRenderer skinnedMeshRenderer in _skinnedMeshRenderers ) {
					if( skinnedMeshRenderer.sharedMesh.blendShapeCount > 0 ) {
						blendShapeAnything = true;
						break;
					}
				}
				if( blendShapeAnything ) {
					_morphBlendShapes = new MorphBlendShape[_skinnedMeshRenderers.Length];
					for( int i = 0; i < _skinnedMeshRenderers.Length; ++i ) {
						_morphBlendShapes[i] = new MorphBlendShape();
						_morphBlendShapes[i].blendShapeIndices = new int[_modelData.morphDataList.Length];
						for( int m = 0; m < _modelData.morphDataList.Length; ++m ) {
							_morphBlendShapes[i].blendShapeIndices[m] = -1;
						}
						SkinnedMeshRenderer skinnedMeshRenderer = _skinnedMeshRenderers[i];
						if( skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.blendShapeCount > 0 ) {
							for( int b = 0; b < skinnedMeshRenderer.sharedMesh.blendShapeCount; ++b ) {
								string blendShapeName = skinnedMeshRenderer.sharedMesh.GetBlendShapeName( b );
								int morphID = MMD4MecanimCommon.ToInt( blendShapeName );
								//Debug.Log ( "Mesh:" + i + " morphID:" + morphID + " blendShapeIndex:" + b + " Name:" + blendShapeName );
								if( (uint)morphID < (uint)_modelData.morphDataList.Length ) {
									_morphBlendShapes[i].blendShapeIndices[morphID] = b;
								}
							}
						}
					}
				}
			}
		}
#endif
	}

	private static void _PostfixRenderQueue( Material[] materials, bool postfixRenderQueue )
	{
		if( Application.isPlaying ) { // Don't change renderQueue in Editor Mode.
			if( materials != null ) {
				for( int i = 0; i < materials.Length; ++i ) {
					if( postfixRenderQueue ) {
						materials[i].renderQueue = 2001 + MMD4MecanimCommon.ToInt( materials[i].name );
					} else {
						if( materials[i].renderQueue == 2999 ) {
							materials[i].renderQueue = 2001;
						}
					}
				}
			}
		}
	}

	private static void _SetupCloneMaterial( CloneMaterial cloneMaterial, Material[] materials )
	{
		cloneMaterial.materials = materials;
		if( materials != null ) {
			int materialLength = materials.Length;
			cloneMaterial.materialData = new MMD4MecanimData.MorphMaterialData[materialLength];
			cloneMaterial.backupMaterialData = new MMD4MecanimData.MorphMaterialData[materialLength];
			cloneMaterial.updateMaterialData = new bool[materialLength];
			for( int i = 0; i < materialLength; ++i ) {
				if( materials[i] != null ) {
					MMD4MecanimCommon.BackupMaterial( ref cloneMaterial.backupMaterialData[i], materials[i] );
					cloneMaterial.materialData[i] = cloneMaterial.backupMaterialData[i];
				}
			}
		}
	}

	private void _InitializeCloneMesh()
	{
		bool initializeCloneMesh = false;
		if( Application.isPlaying ) { // Don't initialize cloneMesh in Editor Mode.
			if( _skinnedMeshRenderers != null && _morphBlendShapes == null ) {
				if( _cloneMeshes == null || _cloneMeshes.Length != _skinnedMeshRenderers.Length ) {
					initializeCloneMesh = true;
					_cloneMeshes = null;
				}
			} else {
				_cloneMeshes = null;
			}
		}

		bool[] validateMesh = null;
		if( initializeCloneMesh ) {
			if( _indexData != null && _indexData.indexValues != null ) {
				int meshCount = _indexData.meshCount;
				if( meshCount > 1 && _skinnedMeshRenderers != null && meshCount == _skinnedMeshRenderers.Length ) {
					int[] indexValues = _indexData.indexValues;

					if( _modelData != null && _modelData.morphDataList != null ) {
						validateMesh = new bool[meshCount];
						for( int m = 0; m < _modelData.morphDataList.Length; ++m ) {
							if( _modelData.morphDataList[m].morphType == MorphType.Vertex ) {
								int[] indices = _modelData.morphDataList[m].indices;
								if( indices != null ) {
									for( int i = 0; i < indices.Length; ++i ) {
										int ofst0 = indexValues[2 + indices[i] + 0];
										int ofst1 = indexValues[2 + indices[i] + 1];
										for( int n = ofst0; n < ofst1; ++n ) {
											uint realIndex = (uint)indexValues[n];
											uint meshIndex = (realIndex >> 24);
											validateMesh[meshIndex] = true;
										}
									}
								}
							}
						}
					}
				}
			}
		}

		int cloneMaterialIndex = 0;
		int cloneMaterialLength = 0;
		if( _meshRenderers != null ) {
			cloneMaterialLength += _meshRenderers.Length;
		}
		if( _skinnedMeshRenderers != null ) {
			cloneMaterialLength += _skinnedMeshRenderers.Length;
		}
		if( cloneMaterialLength > 0 ) {
			_cloneMaterials = new CloneMaterial[cloneMaterialLength];
		}

		if( _meshRenderers != null ) {
			for( int meshIndex = 0; meshIndex < _meshRenderers.Length; ++meshIndex ) {
				MeshRenderer meshRenderer = _meshRenderers[meshIndex];

				Material[] materials = null;
				if( Application.isPlaying ) {
					materials = meshRenderer.materials;
				}
				if( materials == null ) {
					materials = meshRenderer.sharedMaterials;
				}

				_PostfixRenderQueue( materials, this.postfixRenderQueue );

				_cloneMaterials[cloneMaterialIndex] = new CloneMaterial();
				_SetupCloneMaterial( _cloneMaterials[cloneMaterialIndex], materials );
				++cloneMaterialIndex;
			}
		}

		if( _skinnedMeshRenderers != null ) {
			if( initializeCloneMesh ) {
				_cloneMeshes = new CloneMesh[_skinnedMeshRenderers.Length];
			}

			for( int meshIndex = 0; meshIndex < _skinnedMeshRenderers.Length; ++meshIndex ) {
				SkinnedMeshRenderer skinnedMeshRenderer = _skinnedMeshRenderers[meshIndex];

				if( initializeCloneMesh ) {
					if( validateMesh == null || validateMesh[meshIndex] ) {
						_cloneMeshes[meshIndex] = new CloneMesh();
						_cloneMeshes[meshIndex].mesh = MMD4MecanimCommon.CloneMesh( skinnedMeshRenderer.sharedMesh );
						if( _cloneMeshes[meshIndex].mesh != null ) {
							_cloneMeshes[meshIndex].backupVertices = _cloneMeshes[meshIndex].mesh.vertices;
							_cloneMeshes[meshIndex].vertices = _cloneMeshes[meshIndex].backupVertices.Clone() as Vector3[];
							skinnedMeshRenderer.sharedMesh = _cloneMeshes[meshIndex].mesh;
						} else {
							Debug.LogError("CloneMesh() Failed. : " + this.gameObject.name );
						}
					}
				}
				
				Material[] materials = null;
				if( Application.isPlaying ) {
					materials = skinnedMeshRenderer.materials;
				}
				if( materials == null ) {
					materials = skinnedMeshRenderer.sharedMaterials;
				}

				_PostfixRenderQueue( materials, this.postfixRenderQueue );

				_cloneMaterials[cloneMaterialIndex] = new CloneMaterial();
				_SetupCloneMaterial( _cloneMaterials[cloneMaterialIndex], materials );
				++cloneMaterialIndex;
			}
		}

		// Check for Deferred Rendering
		if( _cloneMaterials != null ) {
			for( int i = 0; i < _cloneMaterials.Length; ++i ) {
				Material[] materials = _cloneMaterials[i].materials;
				if( materials != null ) {
					for( int m = 0; m < materials.Length; ++m ) {
						if( MMD4MecanimCommon.IsDeferredShader( materials[m] ) ) {
							_supportDeferred = true;
							break;
						}
					}
					if( _supportDeferred ) {
						break;
					}
				}
			}
		}
	}

	private void _InitializeIndex()
	{
		if( _morphBlendShapes != null ) {
			return; // Skip indexData
		}

		if( _skinnedMeshRenderers == null || _skinnedMeshRenderers.Length == 0 ) {
			return;
		}
		
		if( this.indexFile == null ) {
			Debug.LogWarning( this.gameObject.name + ":indexFile is nothing." );
			return;
		}

		_indexData = MMD4MecanimData.BuildIndexData( this.indexFile );
		if( _indexData == null ) {
			Debug.LogError( this.gameObject.name + ":indexFile is unsupported format." );
			return;
		}
		
		if( !MMD4MecanimData.ValidateIndexData( _indexData, _skinnedMeshRenderers ) ) {
			Debug.LogError( this.gameObject.name + ":indexFile is required recreate." );
			_indexData = null;
			return;
		}
	}
	
	private void _InitializeModel()
	{
		if( this.modelFile == null ) {
			Debug.LogWarning( this.gameObject.name + ":modelFile is nothing." );
			return;
		}
		
		_modelData = MMD4MecanimData.BuildModelData( this.modelFile );
		if( _modelData == null ) {
			Debug.LogError( this.gameObject.name + ":modelFile is unsupported format." );
			return;
		}
		
		if( _modelData.boneDataList != null && _modelData.boneDataDictionary != null ) {
			if( this.boneList == null || this.boneList.Length != _modelData.boneDataList.Length ) {
				this.boneList = new Bone[_modelData.boneDataList.Length];
				_BindBone();

				// Bind(originalParent/target/child/inherenceParent)
				for( int i = 0; i < this.boneList.Length; ++i ) {
					if( this.boneList[i] != null ) {
						this.boneList[i].Bind();
					}
				}
				
				// sortedBoneList
				_sortedBoneList = new Bone[this.boneList.Length];
				for( int i = 0; i < this.boneList.Length; ++i ) {
					if( this.boneList[i] != null ) {
						BoneData boneData = this.boneList[i].boneData;
						if( boneData != null ) {
							int sortedBoneID = boneData.sortedBoneID;
							if( sortedBoneID >= 0 && sortedBoneID < this.boneList.Length ) {
								#if MMD4MECANIM_DEBUG
								if( _sortedBoneList[sortedBoneID] != null ) { // Check overwrite.
									Debug.LogError("");
								}
								#endif
								_sortedBoneList[sortedBoneID] = this.boneList[i];
							} else {
								#if MMD4MECANIM_DEBUG
								Debug.LogError("");
								#endif
							}
						}
					}
				}
			}
		}

		// ikList
		if( _modelData.ikDataList != null ) {
			int ikListLength = _modelData.ikDataList.Length;
			this.ikList = new IK[ikListLength];
			for( int i = 0; i < ikListLength; ++i ) {
				this.ikList[i] = new IK( this, i );
			}
		}

		// morphList
		if( _modelData.morphDataList != null ) {
			this.morphList = new MMD4MecanimModel.Morph[_modelData.morphDataList.Length];
			for( int i = 0; i < _modelData.morphDataList.Length; ++i ) {
				this.morphList[i] = new Morph();
				this.morphList[i].morphData = _modelData.morphDataList[i];
			}
		}
	}

	private void _BindBone()
	{
		Transform transform = this.gameObject.transform;
		foreach( Transform trn in transform ) {
			_BindBone( trn );
		}
	}
	
	private void _BindBone( Transform trn )
	{
		if( !string.IsNullOrEmpty( trn.gameObject.name ) ) {
			int boneID = 0;
			if( _modelData.boneDataDictionary.TryGetValue( trn.gameObject.name, out boneID ) ) {
				MMD4MecanimBone bone = trn.gameObject.GetComponent< MMD4MecanimBone >();
				if( bone == null ) {
					bone = trn.gameObject.AddComponent< MMD4MecanimBone >();
				}
				bone.model = this;
				bone.boneID = boneID;
				bone.Setup();
				this.boneList[boneID] = bone;
				if( this.boneList[boneID].boneData != null && this.boneList[boneID].boneData.isRootBone ) {
					_rootBone = this.boneList[boneID];
				}
			}
		}
		foreach( Transform t in trn ) {
			_BindBone( t );
		}
	}

	void _InitializeAnimatoion()
	{
		this._animator = this.GetComponent< Animator >();

		_animMorphCategoryWeights = new float[(int)MorphCategory.Max];

		if( !Application.isPlaying ) {
			return; // for Editor
		}

		if( _modelData == null ) {
			return;
		}

		bool isEnableAudioClip = false;
		if( this.animList != null ) {
			for( int i = 0; i < this.animList.Length; ++i ) {
				if( this.animList[i] == null ) {
					continue;
				}

				isEnableAudioClip |= (this.animList[i].audioClip != null);
				
				if( this.animList[i].animFile == null ) {
					Debug.LogWarning( this.gameObject.name + ":animFile is nothing." );
					continue;
				}
				
				this.animList[i].animData = MMD4MecanimData.BuildAnimData( this.animList[i].animFile );
				if( this.animList[i].animData == null ) {
					Debug.LogError( this.gameObject.name + ":animFile is unsupported format." );
					continue;
				}

				this.animList[i].animatorStateNameHash = Animator.StringToHash( this.animList[i].animatorStateName );
				
				MMD4MecanimData.MorphMotionData[] morphMotionData = this.animList[i].animData.morphMotionDataList;
				if( morphMotionData != null ) {
					this.animList[i].morphMotionList = new MMD4MecanimModel.Anim.MorphMotion[morphMotionData.Length];
					for( int n = 0; n < morphMotionData.Length; ++n ) {
						this.animList[i].morphMotionList[n].morph = this.GetMorph( morphMotionData[n].name, true );
					}
				}
			}
		}
		
		if( isEnableAudioClip ) {
			GetAudioSource();
		}
	}
	
	void _InitializePhysicsEngine()
	{
		if( this.modelFile == null ) {
			Debug.LogWarning( this.gameObject.name + ":modelFile is nothing." );
			return;
		}
		
		if( this.physicsEngine == PhysicsEngine.BulletPhysics ) {
			MMD4MecanimBulletPhysics instance = MMD4MecanimBulletPhysics.instance;
			if( instance != null ) {
				_bulletPhysicsMMDModel = instance.CreateMMDModel( this );
			}
		}
	}

	void _InitializePPHBones()
	{
		if( _animator == null || _animator.avatar == null || !_animator.avatar.isValid || !_animator.avatar.isHuman ) {
			return;
		}
		{
			Transform leftShoulderTransform = _animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
			Transform leftArmTransform = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
			if( leftShoulderTransform != null && leftArmTransform != null ) {
				PPHBone pphBone = new PPHBone( PPHType.Shoulder, leftShoulderTransform.gameObject );
				pphBone.AddChildSkeleton( leftArmTransform.gameObject );
				_pphBones.Add( pphBone );
			}
		}
		{
			Transform rightShoulderTransform = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
			Transform rightArmTransform = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
			if( rightShoulderTransform != null && rightArmTransform != null ) {
				PPHBone pphBone = new PPHBone( PPHType.Shoulder, rightShoulderTransform.gameObject );
				pphBone.AddChildSkeleton( rightArmTransform.gameObject );
				_pphBones.Add( pphBone );
			}
		}
	}

	static float _FastScl( float weight, float weight2 )
	{
		if( weight2 == 0.0f ) return 0.0f;
		if( weight2 == 1.0f ) return weight;
		return weight * weight2;
	}

	static float _GetMorphUpdateWeight( Morph morph, float[] animMorphCategoryWeights )
	{
		float categoryWeight = animMorphCategoryWeights[(int)morph.morphCategory];
		float animWeight2 = 1.0f - morph.weight2;
		return Mathf.Min( 1.0f,
		    Mathf.Max(
				morph.weight,
				_FastScl( categoryWeight, _FastScl( morph._animWeight + morph._appendWeight, animWeight2 ) ) ) );
	}

	void _UpdateMorph()
	{
		if( this.morphList != null && _animMorphCategoryWeights != null) {
			// Check overrideWeights.
			for( int i = 0; i < _animMorphCategoryWeights.Length; ++i ) {
				_animMorphCategoryWeights[i] = 1.0f;
			}
			for( int i = 0; i < this.morphList.Length; ++i ) {
				Morph morph = this.morphList[i];
				switch( morph.morphCategory ) {
				case MorphCategory.EyeBrow:
				case MorphCategory.Eye:
				case MorphCategory.Lip:
					if( morph.weight2 != 0.0f ) {
						if( morph.weight2 == 1.0f ) {
							_animMorphCategoryWeights[(int)morph.morphCategory] = 0.0f;
						} else {
							_animMorphCategoryWeights[(int)morph.morphCategory] = Mathf.Min( _animMorphCategoryWeights[(int)morph.morphCategory], 1.0f - morph.weight2 );
						}
					}
					break;
				default:
					break;
				}
			}

			// Check update.
			bool updatedAnything = false;
			for( int i = 0; i < this.morphList.Length; ++i ) {
				this.morphList[i]._updateWeight = _GetMorphUpdateWeight( this.morphList[i], _animMorphCategoryWeights );
				updatedAnything |= ( this.morphList[i]._updateWeight != this.morphList[i]._updatedWeight );
			}

			if( updatedAnything ) {
				for( int i = 0; i < this.morphList.Length; ++i ) {
					this.morphList[i]._appendWeight = 0;
				}

				if( _modelData != null && _modelData.morphDataList != null ) {
					bool groupMorphAnything = false;
					for( int i = 0; i < _modelData.morphDataList.Length; ++i ) {
						if( _modelData.morphDataList[i].morphType == MorphType.Group ) {
							groupMorphAnything = true;
							_ApplyMorph( i );
						}
					}
					for( int i = 0; i < this.morphList.Length; ++i ) {
						if( _modelData.morphDataList[i].morphType != MorphType.Group ) {
							if( groupMorphAnything ) {
								this.morphList[i]._updateWeight = _GetMorphUpdateWeight( this.morphList[i], _animMorphCategoryWeights );
							}
							_ApplyMorph( i );
						}
					}
				}

				_UploadMeshVertex();
				_UploadMeshMaterial();
			}
		}
	}
	
	void _ApplyMorph( int morphIndex )
	{
		if( (_morphBlendShapes == null && _cloneMeshes == null) || this.morphList == null || (uint)morphIndex >= (uint)this.morphList.Length ) {
			return;
		}
		
		Morph morph = this.morphList[morphIndex];
		if( morph == null ) {
			return;
		}
		
		float weight = morph._updateWeight;
		morph._updatedWeight = weight;

		if( _modelData == null || _modelData.morphDataList == null || (uint)morphIndex >= (uint)_modelData.morphDataList.Length ) {
			return;
		}
		
		MorphData morphData = _modelData.morphDataList[morphIndex];

		if( morphData.morphType == MorphType.Group ) {
			if( morphData.indices == null ) {
				return;
			}
			for( int i = 0; i < morphData.indices.Length; ++i ) {
				this.morphList[morphData.indices[i]]._appendWeight += weight;
			}
		} else if( morphData.morphType == MorphType.Vertex ) {
#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2
		// Not supported BlendShapes.
#else
			if( _morphBlendShapes != null ) {
				weight *= 100.0f;
				if( _skinnedMeshRenderers != null && _skinnedMeshRenderers.Length == _morphBlendShapes.Length ) {
					for( int i = 0; i < _morphBlendShapes.Length; ++i ) {
						if( _morphBlendShapes[i].blendShapeIndices != null && morphIndex < _morphBlendShapes[i].blendShapeIndices.Length ) {
							int blendShapeIndex = _morphBlendShapes[i].blendShapeIndices[morphIndex];
							if( blendShapeIndex != -1 ) {
								_skinnedMeshRenderers[i].SetBlendShapeWeight( blendShapeIndex, weight );
							}
						}
					}
				}

				return;
			}
#endif
			if( morphData.indices == null ) {
				return;
			}
			if( _indexData == null || _indexData.indexValues == null ) {
				return;
			}
			if( _modelData.vertexCount != _indexData.vertexCount ) {
				return;
			}
			if( morphData.positions == null ) {
				return;
			}

			int[] indexValues = _indexData.indexValues;

			if( Mathf.Abs(weight - 1.0f) <= Mathf.Epsilon ) {
				for( int i = 0; i < morphData.indices.Length; ++i ) {
					int ofst0 = indexValues[2 + morphData.indices[i] + 0];
					int ofst1 = indexValues[2 + morphData.indices[i] + 1];
					for( int n = ofst0; n < ofst1; ++n ) {
						uint realIndex = (uint)indexValues[n];
						uint meshIndex = (realIndex >> 24);
						realIndex &= 0x00ffffff;
						
						CloneMesh cloneMesh = _cloneMeshes[meshIndex];
						if( cloneMesh != null ) {
							if( !cloneMesh.updatedVertices ) {
								cloneMesh.updatedVertices = true;
								System.Array.Copy( cloneMesh.backupVertices, cloneMesh.vertices, cloneMesh.backupVertices.Length );
							}
							Vector3 v = cloneMesh.vertices[realIndex];
							v += morphData.positions[i];
							cloneMesh.vertices[realIndex] = v;
						}
					}
				}
			} else {
				for( int i = 0; i < morphData.indices.Length; ++i ) {
					int ofst0 = indexValues[2 + morphData.indices[i] + 0];
					int ofst1 = indexValues[2 + morphData.indices[i] + 1];
					for( int n = ofst0; n < ofst1; ++n ) {
						uint meshVertexIndex = (uint)indexValues[n];
						uint meshIndex = (meshVertexIndex >> 24);
						meshVertexIndex &= 0x00ffffff;
						
						CloneMesh cloneMesh = _cloneMeshes[meshIndex];
						if( cloneMesh != null ) {
							if( !cloneMesh.updatedVertices ) {
								cloneMesh.updatedVertices = true;
								System.Array.Copy( cloneMesh.backupVertices, cloneMesh.vertices, cloneMesh.backupVertices.Length );
							}
							Vector3 v = cloneMesh.vertices[meshVertexIndex];
							v += morphData.positions[i] * weight;
							cloneMesh.vertices[meshVertexIndex] = v;
						}
					}
				}
			}
		} else if( morphData.morphType == MorphType.Material ) {
			if( morphData.materialData == null ) {
				return;
			}
			for( int i = 0; i < morphData.materialData.Length; ++i ) {
				_ApplyMaterialData( ref morphData.materialData[i], weight );
			}
		}
	}

	void _ApplyMaterialData( ref MMD4MecanimData.MorphMaterialData morphMaterialData, float weight )
	{
		if( _cloneMaterials != null ) {
			foreach( CloneMaterial cloneMaterial in _cloneMaterials ) {
				if( cloneMaterial.backupMaterialData != null && cloneMaterial.updateMaterialData != null && cloneMaterial.materialData != null && cloneMaterial.materials != null ) {
					for( int i = 0; i < cloneMaterial.updateMaterialData.Length; ++i ) {
						if( cloneMaterial.backupMaterialData[i].materialID == morphMaterialData.materialID ) {
							if( !cloneMaterial.updateMaterialData[i] ) {
								cloneMaterial.updateMaterialData[i] = true;
								cloneMaterial.materialData[i] = cloneMaterial.backupMaterialData[i];
							}

							MMD4MecanimCommon.OperationMaterial( ref cloneMaterial.materialData[i], ref morphMaterialData, weight );
						}
					}
				}
			}
		}
	}
	
	void _UploadMeshVertex()
	{
		if( !Application.isPlaying ) {
			return; // Don't initialize cloneMesh for Editor Mode.
		}

		if( _morphBlendShapes != null ) {
			return;
		}

		if( _cloneMeshes != null ) {
			foreach( CloneMesh cloneMesh in _cloneMeshes ) {
				if( cloneMesh != null && cloneMesh.mesh != null && cloneMesh.updatedVertices ) {
					cloneMesh.updatedVertices = false;
					cloneMesh.mesh.vertices = cloneMesh.vertices;
				}
			}
		}
	}

	void _UploadMeshMaterial()
	{
		if( !Application.isPlaying ) {
			return; // Don't initialize cloneMesh for Editor Mode.
		}

		if( _cloneMaterials != null ) {
			foreach( CloneMaterial cloneMaterial in _cloneMaterials ) {
				if( cloneMaterial.updateMaterialData != null && cloneMaterial.materialData != null && cloneMaterial.materials != null ) {
					for( int i = 0; i < cloneMaterial.updateMaterialData.Length; ++i ) {
						if( cloneMaterial.updateMaterialData[i] ) {
							cloneMaterial.updateMaterialData[i] = false;
							MMD4MecanimCommon.FeedbackMaterial( ref cloneMaterial.materialData[i], cloneMaterial.materials[i] );
						}
					}
				}
			}
		}
	}
    int i;
    int show;
    public float DalyTime { set; get; }
    public bool isInit { get; private set; }
    float time1, time2;
	void _UpdateAnim()
	{
		_currentAnim = null;
		if( !this.animEnabled ) {
			return;
		}
		if( this._animator != null && this.animList != null ) {
			AnimatorStateInfo animatorStateInfo = this._animator.GetCurrentAnimatorStateInfo(0);

			int nameHash = animatorStateInfo.fullPathHash;//animatorStateInfo.nameHash;
            float animationTime = animatorStateInfo.normalizedTime * animatorStateInfo.length;
			float f_animationFrameNo = animationTime * 30.0f;
			int animationFrameNo = (int)f_animationFrameNo;
            
            if(false== isInit)
            {
                show = ++i;
                //print("Initing...->" + (show));

                if (show >= 2)
                {
                    if ((DalyTime -= Time.deltaTime) < 0)
                        isInit = true;
                }

                //if (2 == show)
                //{
                //    time1 = Time.realtimeSinceStartup;
                //    print("InitTime:" + time1);
                //}
                //if (3 == show)
                //{
                //    isInit = true;
                //    time2 = Time.realtimeSinceStartup;
                //    print("InitTime:"+ time2);
                //    print("TimeSpan：" + (time2 - time1));
                //}
            }
            for ( int i = 0; i < this.animList.Length; ++i ) {
				_UpdateAnim( this.animList[i], nameHash, animationTime, f_animationFrameNo, animationFrameNo );
			}
		}
	}
	
	void _UpdateAnim( MMD4MecanimModel.Anim animation, int nameHash, float animationTime, float f_frameNo, int frameNo )
	{
		if( animation == null ) {
			return;
		}
		if( string.IsNullOrEmpty(animation.animatorStateName) || animation.animatorStateNameHash != nameHash ) {
			return;
		}

		_currentAnim = animation;
		if( _playingAudioAnim != null && _playingAudioAnim != _currentAnim ) {
			if( this.audioSource != null ) {
				if( this.audioSource.clip == _playingAudioAnim.audioClip ) {
					this.audioSource.Stop();
					this.audioSource.clip = null;
				}
			}
			_playingAudioAnim = null;
		}
		
		if( _playingAudioAnim == null && _currentAnim.audioClip != null ) {
			_playingAudioAnim = _currentAnim;
			if( this.audioSource != null ) {
				if( this.audioSource.clip != _playingAudioAnim.audioClip ) {
					this.audioSource.clip = _playingAudioAnim.audioClip;
					this.audioSource.Play();
				} else {
					if( !this.audioSource.isPlaying ) {
						this.audioSource.Play();
					}
				}
			}
		}
		if( _currentAnim.audioClip != null && this.animSyncToAudio ) {
			if( this.audioSource != null && this.audioSource.isPlaying ) {
				float audioTime = this.audioSource.time;
				if( audioTime == 0.0f ) { // Support for delayed.
					_animator.speed = 0.0f;
				} else {
					float deltaTime = (_prevDeltaTime + Time.deltaTime) * 0.5f;
					float diffTime = audioTime - animationTime;
					if( Mathf.Abs( diffTime ) <= deltaTime ) {
						_animator.speed = 1.0f;
						//Debug.Log( "Safe" );
					} else {
						if( deltaTime > Mathf.Epsilon ) {
							float targetSpeed = 1.0f + diffTime / deltaTime;
							targetSpeed = Mathf.Clamp( targetSpeed, 0.5f, 2.0f );
							if( _animator.speed == 0.0f ) {
								_animator.speed = targetSpeed;
							} else {
								_animator.speed = _animator.speed * 0.95f + targetSpeed * 0.05f;
							}
						} else {
							_animator.speed = 1.0f;
						}
						//Debug.Log( "Unsafe:" + diffTime + ":" + deltaTime + ":" + (diffTime / deltaTime) + ":" + _animator.speed );
					}
				}
			} else {
				_animator.speed = 1.0f;
			}
		}
		
		if( animation.morphMotionList != null && animation.animData != null && animation.animData.morphMotionDataList != null ) {
			for( int i = 0; i < animation.morphMotionList.Length; ++i ) {
				MMD4MecanimModel.Anim.MorphMotion morphMotion = animation.morphMotionList[i];
				MorphMotionData morphMotionData = animation.animData.morphMotionDataList[i];
				if( morphMotion.morph == null ) {
					continue;
				}
				if( morphMotionData.frameNos == null ||
				   morphMotionData.f_frameNos == null ||
				   morphMotionData.weights == null ) {
					continue;
				}
				
				if( morphMotion.lastKeyFrameIndex < morphMotionData.frameNos.Length &&
				   morphMotionData.frameNos[morphMotion.lastKeyFrameIndex] > frameNo ) {
					morphMotion.lastKeyFrameIndex = 0;
				}
				
				bool isProcessed = false;
				for( int keyFrameIndex = morphMotion.lastKeyFrameIndex; keyFrameIndex < morphMotionData.frameNos.Length; ++keyFrameIndex ) {
					int keyFrameNo = morphMotionData.frameNos[keyFrameIndex];
					if( frameNo >= keyFrameNo ) {
						morphMotion.lastKeyFrameIndex = keyFrameIndex;
					} else {
						if( morphMotion.lastKeyFrameIndex + 1 < morphMotionData.frameNos.Length ) {
							_ProcessKeyFrame2( morphMotion.morph, morphMotionData,
							                  morphMotion.lastKeyFrameIndex + 0,
							                  morphMotion.lastKeyFrameIndex + 1,
							                  frameNo, f_frameNo );
						}
						isProcessed = true;
						break;
					}
				}
				if( !isProcessed ) {
					if( morphMotion.lastKeyFrameIndex < morphMotionData.frameNos.Length ) {
						_ProcessKeyFrame( morphMotion.morph, morphMotionData,
						                 morphMotion.lastKeyFrameIndex );
					}
				}
			}
		}
	}

	void _UpdateAnim2()
	{
		if( _playingAudioAnim != null && _currentAnim == null ) {
			if( this.audioSource != null ) {
				if( this.audioSource.clip == _playingAudioAnim.audioClip ) {
					this.audioSource.Stop();
					this.audioSource.clip = null;
				}
			}
			if( _playingAudioAnim.audioClip != null && this.animSyncToAudio ) {
				_animator.speed = 1.0f;
			}
			_playingAudioAnim = null;
		}
	}

	void _ProcessKeyFrame2(
		Morph morph, MorphMotionData motionMorphData,
		int keyFrameIndex0,
		int keyFrameIndex1,
		int frameNo, float f_frameNo )
	{
		int frameNo0 = motionMorphData.frameNos[keyFrameIndex0];
		int frameNo1 = motionMorphData.frameNos[keyFrameIndex1];
		float f_frameNo0 = motionMorphData.f_frameNos[keyFrameIndex0];
		float f_frameNo1 = motionMorphData.f_frameNos[keyFrameIndex1];
		if( frameNo <= frameNo0 || frameNo1 - frameNo0 == 1 ) { /* memo: Don't interpolate adjacent keyframes. */
			morph._animWeight = motionMorphData.weights[keyFrameIndex0];
		} else if( frameNo >= frameNo1 ) {
			morph._animWeight = motionMorphData.weights[keyFrameIndex1];
		} else {
			float r1 = (f_frameNo - f_frameNo0) / (f_frameNo1 - f_frameNo0);
			r1 = Mathf.Clamp( r1, 0.0f, 1.0f );
			float r0 = 1.0f - r1;
			morph._animWeight =
				motionMorphData.weights[keyFrameIndex0] * r0 +
				motionMorphData.weights[keyFrameIndex1] * r1;
		}
	}
	
	void _ProcessKeyFrame( Morph morph, MorphMotionData motionMorphData, int keyFrameIndex )
	{
		morph._animWeight = motionMorphData.weights[keyFrameIndex];
	}
}
