//#define NOUSE_BULLETXNA_UNITY
//#define FORCE_BULLETXNA_UNITY
//#define DEBUG_RIGIDBODY

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PropertyWriter = MMD4MecanimCommon.PropertyWriter;
using BinaryReader = MMD4MecanimCommon.BinaryReader;
using ShapeType = MMD4MecanimData.ShapeType;
using RigidBodyData = MMD4MecanimData.RigidBodyData;
using RigidBodyType = MMD4MecanimData.RigidBodyType;

//[ExecuteInEditMode()]
public class MMD4MecanimBulletPhysics : MonoBehaviour
{
	public static readonly Matrix4x4 rotateMatrixX			= Matrix4x4.TRS( Vector3.zero, Quaternion.Euler( 0.0f, 0.0f, +90.0f ), Vector3.one );
	public static readonly Matrix4x4 rotateMatrixXInv		= Matrix4x4.TRS( Vector3.zero, Quaternion.Euler( 0.0f, 0.0f, -90.0f ), Vector3.one );
	public static readonly Matrix4x4 rotateMatrixZ			= Matrix4x4.TRS( Vector3.zero, Quaternion.Euler( +90.0f, 0.0f, 0.0f ), Vector3.one );
	public static readonly Matrix4x4 rotateMatrixZInv		= Matrix4x4.TRS( Vector3.zero, Quaternion.Euler( -90.0f, 0.0f, 0.0f ), Vector3.one );

	public static readonly Quaternion rotateQuaternionX	= Quaternion.Euler( 0.0f, 0.0f, 90.0f );
	public static readonly Quaternion rotateQuaternionZ	= Quaternion.Euler( 90.0f, 0.0f, 0.0f );
	public static readonly Quaternion rotateQuaternionXInv	= Quaternion.Euler( 0.0f, 0.0f, -90.0f );
	public static readonly Quaternion rotateQuaternionZInv	= Quaternion.Euler( -90.0f, 0.0f, 0.0f );

	[Serializable]
	public class WorldProperty
	{
		public int framePerSecond				= 120;
		public float gravityScale				= 10.0f;
		public float vertexScale				= 8.0f;
		public float importScale				= 0.01f;
		public int worldSolverInfoNumIterations	= 0;
		
		public float worldScale {
			get { return vertexScale * importScale; }
		}
	};

	[Serializable]
	public class MMDModelRigidBodyProperty
	{
		public bool isAdditionalDamping			= true;
		public float mass						= 1.0f;
		public float linearDamping				= 1.0f;
		public float angularDamping				= 1.0f;
		public float restitution				= 1.0f;
		public float friction					= 1.0f;
	};

	[Serializable]
	public class RigidBodyProperty
	{
		public bool isKinematic					= true;
		public bool isAdditionalDamping			= true;
		public float mass						= 1.0f;
		public float linearDamping				= 0.5f;
		public float angularDamping				= 0.5f;
		public float restitution				= 0.5f;
		public float friction					= 0.5f;
	};
	
	public WorldProperty					globalWorldProperty;
	
	private List< MMDModel >				_mmdModelList = new List<MMDModel>();
	private List< RigidBody >				_rigidBodyList = new List<RigidBody>();
	private bool							_isAwaked;
	private World							_globalWorld;
	
	public World globalWorld {
		get {
			_ActivateGlobalWorld();
			return _globalWorld;
		}
	}
	
	static MMD4MecanimBulletPhysics _instance;
	bool _initialized;
	#if !NOUSE_BULLETXNA_UNITY
	bool _isUseBulletXNA;
	public bool isUseBulletXNA { get { return _isUseBulletXNA; } }
	#endif
	public static MMD4MecanimBulletPhysics instance
	{
		get {
			if( _instance == null ) {
				_instance = (MMD4MecanimBulletPhysics)MonoBehaviour.FindObjectOfType( typeof(MMD4MecanimBulletPhysics) );
				if( _instance == null ) {
					GameObject gameObject = new GameObject("MMD4MecanimBulletPhysics");
					MMD4MecanimBulletPhysics instance = gameObject.AddComponent<MMD4MecanimBulletPhysics>();
					if( _instance == null ) {
						_instance = instance;
					}
				}
				if( _instance != null ) {
					_instance._Initialize();
				}
			}

			return _instance;
		}
	}
	
	private void _Initialize()
	{
		if( _initialized ) {
			return;
		}
		_initialized = true;
		DontDestroyOnLoad( this.gameObject );

		#if !NOUSE_BULLETXNA_UNITY
		if( Application.HasProLicense() ) {
			#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
			_isUseBulletXNA = false;
			#else
			_isUseBulletXNA = true;
			#endif
		} else {
			_isUseBulletXNA = true;
		}
		#if FORCE_BULLETXNA_UNITY
		_isUseBulletXNA = true;
		#endif
		if( _isUseBulletXNA ) {
			Debug.Log( "MMD4MecanimBulletPhysics:Awake BulletXNA." );
		} else {
			Debug.Log( "MMD4MecanimBulletPhysics:Awake Native Plugin." );
		}
		#endif

		// http://docs.unity3d.com/Documentation/Manual/ExecutionOrder.html
		StartCoroutine( DelayedAwake() );
	}
	
	public class World
	{
		public WorldProperty					worldProperty;
		public IntPtr							worldPtr;
		#if !NOUSE_BULLETXNA_UNITY
		public MMD4MecanimBulletPhysicsWorld	bulletPhysicsWorld;
		#endif

		~World()
		{
			Destroy();
		}

		public bool isExpired
		{
			get {
				#if !NOUSE_BULLETXNA_UNITY
				if( this.bulletPhysicsWorld != null ) {
					return false;
				}
				#endif
				return this.worldPtr == IntPtr.Zero;
			}
		}

		public bool Create()
		{
			return Create( null );
		}
		
		public bool Create( WorldProperty worldProperty )
		{
			Destroy();

			if( worldProperty != null ) {
				this.worldProperty = worldProperty;
			} else {
				this.worldProperty = new WorldProperty();
			}

			#if !NOUSE_BULLETXNA_UNITY
			if( MMD4MecanimBulletPhysics.instance != null && MMD4MecanimBulletPhysics.instance.isUseBulletXNA ) {
				MMD4MecanimBulletPhysicsWorld.CreateProperty createProperty = new MMD4MecanimBulletPhysicsWorld.CreateProperty();
				createProperty.framePerSecond = this.worldProperty.framePerSecond;
				createProperty.gravityScale = this.worldProperty.gravityScale;
				createProperty.worldSolverInfoNumIterations = this.worldProperty.worldSolverInfoNumIterations;
				this.bulletPhysicsWorld = new MMD4MecanimBulletPhysicsWorld();
				if( !this.bulletPhysicsWorld.Create( ref createProperty ) ) {
					this.bulletPhysicsWorld.Destroy();
					this.bulletPhysicsWorld = null;
					return false;
				}
				return true;
			}
			#endif
			
			if( this.worldProperty != null ) {
				int[] iValues = new int[7];
				float[] fValues = new float[3];
				
				int i = 0, f = 0;
				iValues[i] = MMD4MecanimCommon.MurmurHash32("framePerSecond"); ++i;
				iValues[i] = this.worldProperty.framePerSecond; ++i;
				iValues[i] = MMD4MecanimCommon.MurmurHash32("gravityScale"); ++i;
				fValues[f] = this.worldProperty.gravityScale; ++f;
				iValues[i] = MMD4MecanimCommon.MurmurHash32("vertexScale"); ++i;
				fValues[f] = this.worldProperty.vertexScale; ++f;
				iValues[i] = MMD4MecanimCommon.MurmurHash32("importScale"); ++i;
				fValues[f] = this.worldProperty.importScale; ++f;
				iValues[i]= MMD4MecanimCommon.MurmurHash32("worldSolverInfoNumIterations"); ++i;
				iValues[i] = this.worldProperty.worldSolverInfoNumIterations; ++i;
				
				GCHandle gch_iValues = GCHandle.Alloc(iValues, GCHandleType.Pinned);
				GCHandle gch_fValues = GCHandle.Alloc(fValues, GCHandleType.Pinned);
				this.worldPtr = _CreateWorld(
					gch_iValues.AddrOfPinnedObject(), iValues.Length,
					gch_fValues.AddrOfPinnedObject(), fValues.Length );
				gch_fValues.Free();
				gch_iValues.Free();
			} else {
				this.worldPtr = _CreateWorld( IntPtr.Zero, 0, IntPtr.Zero, 0 );
			}
			
			if( MMD4MecanimBulletPhysics.instance != null ) {
				MMD4MecanimBulletPhysics.instance.DebugLog();
			}

			return ( this.worldPtr != IntPtr.Zero );
		}
		
		public void Destroy()
		{
			#if !NOUSE_BULLETXNA_UNITY
			if( this.bulletPhysicsWorld != null ) {
				this.bulletPhysicsWorld.Destroy();
				this.bulletPhysicsWorld = null;
			}
			#endif
			if( this.worldPtr != IntPtr.Zero ) {
				_DestroyWorld( this.worldPtr );
				if( MMD4MecanimBulletPhysics.instance != null ) {
					MMD4MecanimBulletPhysics.instance.DebugLog();
				}
				this.worldPtr = IntPtr.Zero;
			}
			this.worldProperty = null;
		}
		
		public void Update( float deltaTime )
		{
			#if !NOUSE_BULLETXNA_UNITY
			if( this.bulletPhysicsWorld != null ) {
				this.bulletPhysicsWorld.Update( deltaTime );
			}
			#endif
			if( this.worldPtr != IntPtr.Zero ) {
				_UpdateWorld( this.worldPtr, deltaTime );
			}
		}
	}
	
	public class RigidBody
	{
		public MMD4MecanimRigidBody				rigidBody;
		public IntPtr							rigidBodyPtr;
		#if !NOUSE_BULLETXNA_UNITY
		public MMD4MecanimBulletRigidBody		bulletRigidBody;
		#endif

		private float[]							fValues = new float[8];

		private SphereCollider					_sphereCollider;
		private BoxCollider						_boxCollider;
		private CapsuleCollider					_capsuleCollider;
		
		private Vector3 _center {
			get {
				if( _sphereCollider != null ) {
					return _sphereCollider.center;
				} else if( _boxCollider != null ) {
					return _boxCollider.center;
				} else if( _capsuleCollider != null ) {
					return _capsuleCollider.center;
				}
				return Vector3.zero;
			}
		}
		
		~RigidBody()
		{
			Destroy();
		}
		
		public bool isExpired
		{
			get {
				#if !NOUSE_BULLETXNA_UNITY
				if( this.bulletRigidBody != null ) {
					return false;
				}
				#endif
				return this.rigidBodyPtr == IntPtr.Zero;
			}
		}
		
		public bool Create( MMD4MecanimRigidBody rigidBody )
		{
			Destroy();

			if( rigidBody == null ) {
				return false;
			}
			
			World joinWorld = null;
			if( MMD4MecanimBulletPhysics.instance != null ) {
				joinWorld = MMD4MecanimBulletPhysics.instance.globalWorld;
			}
			if( joinWorld == null ) {
				return false;
			}

			#if !NOUSE_BULLETXNA_UNITY
			MMD4MecanimBulletRigidBody.CreateProperty createProperty = new MMD4MecanimBulletRigidBody.CreateProperty();
			bool isUseBulletXNA = (MMD4MecanimBulletPhysics.instance != null && MMD4MecanimBulletPhysics.instance.isUseBulletXNA);
			#endif

			#if !NOUSE_BULLETXNA_UNITY
			PropertyWriter propertyWriter = isUseBulletXNA ? null : (new PropertyWriter());
			#else
			PropertyWriter propertyWriter = new PropertyWriter();
			#endif

			Matrix4x4 matrix = rigidBody.transform.localToWorldMatrix;
			Vector3 position = rigidBody.transform.position;
			Quaternion rotation = rigidBody.transform.rotation;
			Vector3 scale = MMD4MecanimCommon.ComputeMatrixScale( ref matrix );

			Vector3 center = this._center;
			if( center != Vector3.zero ) {
				position = matrix.MultiplyPoint3x4( center );
			}

			SphereCollider sphereCollider = rigidBody.gameObject.GetComponent< SphereCollider >();
			if( sphereCollider != null ) {
				float radiusSize = sphereCollider.radius;
				radiusSize *= Mathf.Max( Mathf.Max( scale.x, scale.y ), scale.z );
				if( propertyWriter != null ) {
					propertyWriter.Write( "shapeType", 0 );
					propertyWriter.Write( "shapeSize", new Vector3( radiusSize, 0.0f, 0.0f ) );
				}
				#if !NOUSE_BULLETXNA_UNITY
				if( isUseBulletXNA ) {
					createProperty.shapeType = 0;
					createProperty.shapeSize = new Vector3( radiusSize, 0.0f, 0.0f );
				}
				#endif
			}
			BoxCollider boxCollider = rigidBody.gameObject.GetComponent< BoxCollider >();
			if( boxCollider != null ) {
				Vector3 boxSize = boxCollider.size;
				boxSize.x *= scale.x;
				boxSize.y *= scale.y;
				boxSize.z *= scale.z;
				if( propertyWriter != null ) {
					propertyWriter.Write( "shapeType", 1 );
					propertyWriter.Write( "shapeSize", boxSize * 0.5f );
				}
				#if !NOUSE_BULLETXNA_UNITY
				if( isUseBulletXNA ) {
					createProperty.shapeType = 1;
					createProperty.shapeSize = boxSize * 0.5f;
				}
				#endif
			}
			CapsuleCollider capsuleCollider = rigidBody.gameObject.GetComponent< CapsuleCollider >();
			if( capsuleCollider != null ) {
				Vector3 capsuleSize = new Vector3( capsuleCollider.radius, capsuleCollider.height, 0.0f );
				capsuleSize.x *= Mathf.Max( scale.x, scale.z );
				capsuleSize.y *= scale.y;
				capsuleSize.y -= capsuleCollider.radius * 2.0f;
				if( propertyWriter != null ) {
					propertyWriter.Write( "shapeType", 2 );
					propertyWriter.Write( "shapeSize", capsuleSize );
				}
				#if !NOUSE_BULLETXNA_UNITY
				if( isUseBulletXNA ) {
					createProperty.shapeType = 2;
					createProperty.shapeSize = capsuleSize;
				}
				#endif
			}
			_sphereCollider		= sphereCollider;
			_boxCollider		= boxCollider;
			_capsuleCollider	= capsuleCollider;

			if( capsuleCollider != null ) {
				if( capsuleCollider.direction == 0 ) { // X axis
					rotation *= rotateQuaternionX;
				} else if( capsuleCollider.direction == 2 ) { // Z axis
					rotation *= rotateQuaternionZ;
				}
			}

			if( joinWorld.worldProperty != null ) {
				if( propertyWriter != null ) {
					propertyWriter.Write( "unityScale", joinWorld.worldProperty.worldScale );
				}
				#if !NOUSE_BULLETXNA_UNITY
				if( isUseBulletXNA ) {
					createProperty.unityScale = joinWorld.worldProperty.worldScale;
				}
				#endif
			} else {
				if( propertyWriter != null ) {
					propertyWriter.Write( "unityScale", 1.0f );
				}
				#if !NOUSE_BULLETXNA_UNITY
				if( isUseBulletXNA ) {
					createProperty.unityScale = 1.0f;
				}
				#endif
			}

			position.x = -position.x;
			rotation.y = -rotation.y;
			rotation.z = -rotation.z;

			if( propertyWriter != null ) {
				propertyWriter.Write( "position",	position );
				propertyWriter.Write( "rotation",	rotation );
			}
			#if !NOUSE_BULLETXNA_UNITY
			if( isUseBulletXNA ) {
				createProperty.position = position;
				createProperty.rotation = rotation;
			}
			#endif

			int rigidBodyFlags = 0;
			if( rigidBody.bulletPhysicsRigidBodyProperty != null ) {
				if( !rigidBody.bulletPhysicsRigidBodyProperty.isKinematic ) {
					rigidBodyFlags = 0x01;
				}
				if( rigidBody.bulletPhysicsRigidBodyProperty.isAdditionalDamping ) {
					rigidBodyFlags |= 0x04;
				}
				#if !NOUSE_BULLETXNA_UNITY
				if( isUseBulletXNA ) {
					createProperty.isKinematic = rigidBody.bulletPhysicsRigidBodyProperty.isKinematic;
					createProperty.additionalDamping = rigidBody.bulletPhysicsRigidBodyProperty.isAdditionalDamping;
				}
				#endif

				float mass = rigidBody.bulletPhysicsRigidBodyProperty.mass;
				if( rigidBody.bulletPhysicsRigidBodyProperty.isKinematic ) {
					mass = 0.0f; // Hotfix: Todo: Move to plugin/classes
				}
				
				if( propertyWriter != null ) {
					propertyWriter.Write( "mass",			mass );
					propertyWriter.Write( "linearDamping",	rigidBody.bulletPhysicsRigidBodyProperty.linearDamping );
					propertyWriter.Write( "angularDamping",	rigidBody.bulletPhysicsRigidBodyProperty.angularDamping );
					propertyWriter.Write( "restitution",	rigidBody.bulletPhysicsRigidBodyProperty.restitution );
					propertyWriter.Write( "friction",		rigidBody.bulletPhysicsRigidBodyProperty.friction );
				}
				#if !NOUSE_BULLETXNA_UNITY
				if( isUseBulletXNA ) {
					createProperty.mass = mass;
					createProperty.linearDamping = rigidBody.bulletPhysicsRigidBodyProperty.linearDamping;
					createProperty.angularDamping = rigidBody.bulletPhysicsRigidBodyProperty.angularDamping;
					createProperty.restitution = rigidBody.bulletPhysicsRigidBodyProperty.restitution;
					createProperty.friction = rigidBody.bulletPhysicsRigidBodyProperty.friction;
				}
				#endif
			}

			if( propertyWriter != null ) {
				propertyWriter.Write( "flags", rigidBodyFlags );
				propertyWriter.Write( "group", 65535 );
				propertyWriter.Write( "mask", 65535 );
			}
			#if !NOUSE_BULLETXNA_UNITY
			if( isUseBulletXNA ) {
				createProperty.group = 65535;
				createProperty.mask = 65535;
			}
			#endif

			#if !NOUSE_BULLETXNA_UNITY
			if( isUseBulletXNA ) {
				this.bulletRigidBody = new MMD4MecanimBulletRigidBody();
				if( !this.bulletRigidBody.Create( ref createProperty ) ) {
					this.bulletRigidBody.Destroy();
					this.bulletRigidBody = null;
					return false;
				}
				if( joinWorld.bulletPhysicsWorld != null ) {
					joinWorld.bulletPhysicsWorld.JoinWorld( this.bulletRigidBody );
				}
				this.rigidBody = rigidBody;
				return true;
			}
			#endif

			propertyWriter.Lock();
			IntPtr rigidBodyPtr = _CreateRigidBody(
				propertyWriter.iValuesPtr, propertyWriter.iValueLength,
				propertyWriter.fValuesPtr, propertyWriter.fValueLength );
			propertyWriter.Unlock();
			
			if( rigidBodyPtr != IntPtr.Zero ) {
				_JoinWorldRigidBody( joinWorld.worldPtr, rigidBodyPtr );
				if( MMD4MecanimBulletPhysics.instance != null ) {
					MMD4MecanimBulletPhysics.instance.DebugLog();
				}
				this.rigidBody = rigidBody;
				this.rigidBodyPtr = rigidBodyPtr;
				return true;
			} else {
				if( MMD4MecanimBulletPhysics.instance != null ) {
					MMD4MecanimBulletPhysics.instance.DebugLog();
				}
				return false;
			}
		}
		
		public void Update()
		{
			if( rigidBody != null && rigidBody.bulletPhysicsRigidBodyProperty != null ) {
				if( rigidBody.bulletPhysicsRigidBodyProperty.isKinematic ) {
					Vector3 position = rigidBody.transform.position;
					Quaternion rotation = rigidBody.transform.rotation;
					
					Vector3 center = this._center;
					if( center != Vector3.zero ) {
						position = rigidBody.transform.localToWorldMatrix.MultiplyPoint3x4( center );
					}
					
					if( _capsuleCollider != null ) {
						if( _capsuleCollider.direction == 0 ) { // X axis
							rotation *= rotateQuaternionX;
						} else if( _capsuleCollider.direction == 2 ) { // Z axis
							rotation *= rotateQuaternionZ;
						}
					}

					position.x = -position.x;
					rotation.y = -rotation.y;
					rotation.z = -rotation.z;

					#if !NOUSE_BULLETXNA_UNITY
					if( this.bulletRigidBody != null ) {
						this.bulletRigidBody.Update( ref position, ref rotation );
					}
					#endif
					if( rigidBodyPtr != IntPtr.Zero ) {
						fValues[0] = position.x;
						fValues[1] = position.y;
						fValues[2] = position.z;
						fValues[3] = 1.0f;
						fValues[4] = rotation.x;
						fValues[5] = rotation.y;
						fValues[6] = rotation.z;
						fValues[7] = rotation.w;

						GCHandle gch_fValues = GCHandle.Alloc(fValues, GCHandleType.Pinned);
						_UpdateRigidBody( rigidBodyPtr, IntPtr.Zero, 0, gch_fValues.AddrOfPinnedObject(), fValues.Length );
						gch_fValues.Free();
					}
				}
			}
		}
		
		public void LateUpdate()
		{
			if( rigidBody != null && rigidBody.bulletPhysicsRigidBodyProperty != null ) {
				if( !rigidBody.bulletPhysicsRigidBodyProperty.isKinematic ) {
					Vector3 position = Vector3.one;
					Quaternion rotation = Quaternion.identity;

					#if !NOUSE_BULLETXNA_UNITY
					if( this.bulletRigidBody != null ) {
						this.bulletRigidBody.LateUpdate( ref position, ref rotation );
						position.x = -position.x;
						rotation.y = -rotation.y;
						rotation.z = -rotation.z;
					}
					#endif
					if( rigidBodyPtr != IntPtr.Zero ) {
						GCHandle gch_fValues = GCHandle.Alloc(fValues, GCHandleType.Pinned);
						_LateUpdateRigidBody( rigidBodyPtr, IntPtr.Zero, 0, gch_fValues.AddrOfPinnedObject(), fValues.Length );
						gch_fValues.Free();

						position = new Vector3( -fValues[0], fValues[1], fValues[2] );
						rotation = new Quaternion( fValues[4], -fValues[5], -fValues[6], fValues[7] );
					}

					if( _capsuleCollider != null ) {
						if( _capsuleCollider.direction == 0 ) { // X axis
							rotation *= rotateQuaternionXInv;
						} else if( _capsuleCollider.direction == 2 ) { // Z axis
							rotation *= rotateQuaternionZInv;
						}
					}

					rigidBody.gameObject.transform.position = position;
					rigidBody.gameObject.transform.rotation = rotation;

					Vector3 center = this._center;
					if( center != Vector3.zero ) {
						Vector3 localPosition = rigidBody.gameObject.transform.localPosition;
						localPosition -= center;
						rigidBody.gameObject.transform.localPosition = localPosition;
					}
				}
			}
		}
		
		public void Destroy()
		{
			#if !NOUSE_BULLETXNA_UNITY
			if( this.bulletRigidBody != null ) {
				this.bulletRigidBody.Destroy();
				this.bulletRigidBody = null;
			}
			#endif
			if( this.rigidBodyPtr != IntPtr.Zero ) {
				_DestroyRigidBody( this.rigidBodyPtr );
				this.rigidBodyPtr = IntPtr.Zero;
			}

			_sphereCollider		= null;
			_boxCollider		= null;
			_capsuleCollider	= null;
			this.rigidBody		= null;
		}
	};
	
	public class MMDModel
	{
		public bool								resetWorld;
		public World							localWorld;
		public MMD4MecanimModel					model;

		#if !NOUSE_BULLETXNA_UNITY
		public MMD4MecanimBulletPMXModel		bulletPMXModel;
		#endif
		public IntPtr							mmdModelPtr;
		public GameObject						physics;
		public Bone[]							boneList;
		public RigidBody[]						rigidBodyList;

		private Vector3							_scale = Vector3.one;
		private Vector3							_rScale = Vector3.one;
		private bool							_identityScale = true;
		private int[]							_kinematicFlagsList;
		private float[]							_kinematicTransformList;
		private int[]							_nonKinematicFlagsList;
		private float[]							_nonKinematicTransformList;

		private float 							_modelToUnityScale = 1.0f;
		private float							_modelToBulletScale = 1.0f;
		private float							_bulletToUnityScale = 1.0f;
		private float							_unityToBulletScale = 1.0f;

		public float modelToUnityScale { get { return _modelToUnityScale; } }
		public float modelToBulletScale { get { return _modelToBulletScale; } }
		public float bulletToUnityScale { get { return _bulletToUnityScale; } }
		public float unityToBulletScale { get { return _unityToBulletScale; } }

		public class Bone
		{
			public int				boneID;
			public GameObject		gameObject;
			public bool				isRigidBody;
			public bool				isKinematic;
		}

		public class RigidBody
		{
			public Bone				bone;
			public int				rigidBodyID;
			public GameObject		gameObject;
			public RigidBodyData	rigidBodyData;
		}

		~MMDModel()
		{
			Destroy();
		}
		
		public bool isExpired
		{
			get {
				#if !NOUSE_BULLETXNA_UNITY
				if( this.bulletPMXModel != null ) {
					return false;
				}
				#endif
				return this.mmdModelPtr == IntPtr.Zero && this.localWorld == null;
			}
		}
		
		private bool _Prepare( MMD4MecanimModel model )
		{
			if( model == null ) {
				return false;
			}

			this.model = model;

			this.physics = model.gameObject;
			MMD4MecanimData.ModelData modelData = model.modelData;
			if( modelData == null || modelData.boneDataList == null ||
				model.boneList == null || model.boneList.Length != modelData.boneDataList.Length ) {
				Debug.LogError( "_Prepare: Failed." );
				return false;
			}

			_modelToUnityScale = modelData.vertexScale * modelData.importScale; // Unity < Mesh Scale

			if( modelData.boneDataList != null ) {
				int boneListLength	= modelData.boneDataList.Length;
				this.boneList		= new Bone[boneListLength];
				for( int i = 0; i < boneListLength; ++i ) {
					if( model.boneList[i] != null && model.boneList[i].gameObject != null ) {
						Bone bone = new Bone();
						bone.gameObject = model.boneList[i].gameObject;
						bone.boneID = i;
						bone.isRigidBody = modelData.boneDataList[i].isRigidBody;
						bone.isKinematic = modelData.boneDataList[i].isKinematic;
						this.boneList[i] = bone;
					}
				}
			}

			#if DEBUG_RIGIDBODY
			if( modelData.rigidBodyDataList != null ) {
				int rigidBodyLength	= modelData.rigidBodyDataList.Length;
				this.rigidBodyList	= new RigidBody[rigidBodyLength];
				for( int i = 0; i < rigidBodyLength; ++i ) {
					RigidBodyData rigidBodyData = modelData.rigidBodyDataList[i];
					RigidBody rigidBody = new RigidBody();
					rigidBody.rigidBodyID = i;
					rigidBody.rigidBodyData = rigidBodyData;
					if( this.boneList != null && (uint)rigidBodyData.boneID < this.boneList.Length ) {
						rigidBody.bone = this.boneList[rigidBodyData.boneID];
					}
					this.rigidBodyList[i] = rigidBody;
				}
			}
			#endif

			_PrepareWork();
			return true;
		}
		
		public bool Create( MMD4MecanimModel model )
		{
			if( model == null ) {
				return false;
			}
			byte[] mmdModelBytes = model.modelFileBytes;
			if( mmdModelBytes == null ) {
				Debug.LogError("");
				return false;
			}
			if( !_Prepare( model ) ) {
				Debug.LogError("");
				return false;
			}

			Matrix4x4 matrix = physics.gameObject.transform.localToWorldMatrix;
			_scale = MMD4MecanimCommon.ComputeMatrixScale( ref matrix );
			_identityScale = Mathf.Abs( 1.0f - _scale.x ) <= 0.0001f
							&& Mathf.Abs( 1.0f - _scale.y ) <= 0.0001f
							&& Mathf.Abs( 1.0f - _scale.z ) <= 0.0001f;
			_rScale = _identityScale ? Vector3.one : MMD4MecanimCommon.Reciplocal( ref _scale );

			bool joinLocalWorld = true;
			bool useOriginalScale = true;
			bool useCustomResetTime = false;
			float resetMorphTime = 0.0f;
			float resetWaitTime = 0.0f;
			MMD4MecanimBulletPhysics.MMDModelRigidBodyProperty mmdModelRigidBodyProperty = null;
			MMD4MecanimBulletPhysics.WorldProperty localWorldProperty = null;
			if( model.bulletPhysics != null ) {
				mmdModelRigidBodyProperty = model.bulletPhysics.mmdModelRigidBodyProperty;
				localWorldProperty = model.bulletPhysics.worldProperty;
				joinLocalWorld = model.bulletPhysics.joinLocalWorld;
				useOriginalScale = model.bulletPhysics.useOriginalScale;
				useCustomResetTime = model.bulletPhysics.useCustomResetTime;
				resetMorphTime = model.bulletPhysics.resetMorphTime;
				resetWaitTime = model.bulletPhysics.resetWaitTime;
			}

			float unityScale = 0.0f;
			World joinWorld = null;
			World localWorld = null;
			if( joinLocalWorld ) {
				if( localWorldProperty == null ) {
					Debug.LogError( "localWorldProperty is null." );
					return false;
				}
				
				localWorld = new World();
				joinWorld = localWorld;

				if( !localWorld.Create( localWorldProperty ) ) {
					Debug.LogError("");
					return false;
				}
				
				if( useOriginalScale ) {
					float worldScale = model.modelData.vertexScale * model.modelData.importScale;
					unityScale = _scale.x * worldScale;
				} else {
					unityScale = _scale.x * localWorldProperty.worldScale;
				}
			} else {
				if( MMD4MecanimBulletPhysics.instance != null ) {
					joinWorld = MMD4MecanimBulletPhysics.instance.globalWorld;
				}
				if( joinWorld == null ) {
					Debug.LogError("");
					return false;
				}
				if( joinWorld.worldProperty == null ) {
					Debug.LogError( "worldProperty is null." );
					return false;
				}

				unityScale = _scale.x * joinWorld.worldProperty.worldScale;
			}

			_modelToBulletScale = 1.0f;
			_bulletToUnityScale = 1.0f;
			_unityToBulletScale = 1.0f;
			
			if( unityScale > Mathf.Epsilon ) {
				_bulletToUnityScale = unityScale;
			} else {
				_bulletToUnityScale = _modelToUnityScale;
			}
			
			if( _bulletToUnityScale > Mathf.Epsilon ) {
				_unityToBulletScale = 1.0f / _bulletToUnityScale;
			}
			
			_modelToBulletScale = _unityToBulletScale * _modelToUnityScale;

			#if DEBUG_RIGIDBODY
			if( this.rigidBodyList != null ) {
				for( int i = 0; i < this.rigidBodyList.Length; ++i ) {
					RigidBodyData rigidBodyData = this.rigidBodyList[i].rigidBodyData;
					if( rigidBodyData != null ) {
						if( this.rigidBodyList[i].bone != null && this.rigidBodyList[i].bone.gameObject != null ) {
							Vector3 shapeSize = rigidBodyData.shapeSize * _modelToBulletScale * _bulletToUnityScale;
							Vector3 position = rigidBodyData.position * _modelToBulletScale;
							Vector3 rotation = rigidBodyData.rotation;

							GameObject parentGameObject = this.rigidBodyList[i].bone.gameObject;

							GameObject rigidBodyGameObject = new GameObject("Coll." + parentGameObject.name);
							rigidBodyGameObject.transform.parent = parentGameObject.transform;
							rigidBodyGameObject.transform.localPosition = Vector3.zero;
							rigidBodyGameObject.transform.localRotation = Quaternion.identity;
							rigidBodyGameObject.transform.localScale = Vector3.one;

							// LH to RH
							position.z = -position.z;
							rotation.x = -rotation.x;
							rotation.y = -rotation.y;

							BulletXNA.LinearMath.IndexedMatrix boneTransform;

							boneTransform._basis = MMD4MecanimBulletPhysicsUtil.BasisRotationYXZ( ref rotation );
							boneTransform._origin = position;

							Vector3 geomPosition = boneTransform._origin * _bulletToUnityScale;
							var geomRotation = boneTransform.GetRotation();

							Quaternion quaternion = new Quaternion(
								geomRotation.X,
								-geomRotation.Y,
								-geomRotation.Z,
								geomRotation.W );
							
							rigidBodyGameObject.transform.localRotation = quaternion;
							rigidBodyGameObject.transform.localPosition = new Vector3( -geomPosition[0], geomPosition[1], geomPosition[2] );

							switch( rigidBodyData.shapeType ) {
							case ShapeType.Sphere:
								{
									SphereCollider sphereCollider = rigidBodyGameObject.AddComponent<SphereCollider>();
									sphereCollider.radius = shapeSize.x;
								}
								break;
							case ShapeType.Box:
								{
									BoxCollider boxCollider = rigidBodyGameObject.AddComponent<BoxCollider>();
									boxCollider.size = shapeSize * 2.0f;
								}
								break;
							case ShapeType.Capsule:
								{
									CapsuleCollider capsuleCollider = rigidBodyGameObject.AddComponent<CapsuleCollider>();
									capsuleCollider.radius = shapeSize.x;
									capsuleCollider.height = shapeSize.y + shapeSize.x * 2.0f;

								//Vector3 capsuleSize = new Vector3( capsuleCollider.radius, capsuleCollider.height, 0.0f );
								//capsuleSize.x *= Mathf.Max( scale.x, scale.z );
								//capsuleSize.y *= scale.y;
								//capsuleSize.y -= capsuleCollider.radius * 2.0f;

								}
								break;
							}
						}
					}
				}
			}
			#endif

			#if !NOUSE_BULLETXNA_UNITY
			bool isUseBulletXNA = (MMD4MecanimBulletPhysics.instance != null && MMD4MecanimBulletPhysics.instance.isUseBulletXNA);
			if( isUseBulletXNA ) {
				MMD4MecanimBulletPMXModel.ImportProperty importProperty = new MMD4MecanimBulletPMXModel.ImportProperty();
				importProperty.unityScale = unityScale;
				importProperty.useCustomResetTime = useCustomResetTime;
				importProperty.resetMorphTime = resetMorphTime;
				importProperty.resetWaitTime = resetWaitTime;
				importProperty.mmdModelRigidBodyProperty = mmdModelRigidBodyProperty;

				MMD4MecanimCommon.BinaryReader binaryReader = new BinaryReader( mmdModelBytes );
				if( !binaryReader.Preparse() ) {
					Debug.LogError("");
					if( localWorld != null ) {
						localWorld.Destroy();
					}
					return false;
				}

				this.bulletPMXModel = new MMD4MecanimBulletPMXModel();
				if( !this.bulletPMXModel.Import( binaryReader, ref importProperty ) ) {
					Debug.LogError("");
					this.bulletPMXModel.Destroy();
					if( localWorld != null ) {
						localWorld.Destroy();
					}
					return false;
				}

				if( joinWorld != null && joinWorld.bulletPhysicsWorld != null ) {
					joinWorld.bulletPhysicsWorld.JoinWorld( this.bulletPMXModel );
				}
				this.localWorld = localWorld;
				this.physics = model.gameObject;
				return true;
			}
			#endif

			MMD4MecanimCommon.PropertyWriter property = new MMD4MecanimCommon.PropertyWriter();
			property.Write( "unityScale", unityScale );
			if( useCustomResetTime ) {
				property.Write( "resetMorphTime", resetMorphTime );
				property.Write( "resetWaitTime", resetWaitTime );
			}
			if( mmdModelRigidBodyProperty != null ) {
				property.Write( "rigidBodyIsAdditionalDamping", mmdModelRigidBodyProperty.isAdditionalDamping );
				property.Write( "rigidBodyMass", mmdModelRigidBodyProperty.mass );
				property.Write( "rigidBodyLinearDamping", mmdModelRigidBodyProperty.linearDamping );
				property.Write( "rigidBodyAngularDamping", mmdModelRigidBodyProperty.angularDamping );
				property.Write( "rigidBodyRestitution", mmdModelRigidBodyProperty.restitution );
				property.Write( "rigidBodyFriction", mmdModelRigidBodyProperty.friction );
			}

			property.Lock();
			GCHandle gch_mmdModel = GCHandle.Alloc(mmdModelBytes, GCHandleType.Pinned);
			IntPtr mmdModelPtr = _CreateMMDModel(
				gch_mmdModel.AddrOfPinnedObject(), mmdModelBytes.Length,
				property.iValuesPtr, property.iValueLength,
				property.fValuesPtr, property.fValueLength );
			gch_mmdModel.Free();
			property.Unlock();

			if( mmdModelPtr != IntPtr.Zero ) {
				_JoinWorldMMDModel( joinWorld.worldPtr, mmdModelPtr );
				if( MMD4MecanimBulletPhysics.instance != null ) {
					MMD4MecanimBulletPhysics.instance.DebugLog();
				}
				this.localWorld = localWorld;
				this.mmdModelPtr = mmdModelPtr;
				this.physics = model.gameObject;
				return true;
			} else {
				if( localWorld != null ) {
					localWorld.Destroy();
				}
				if( MMD4MecanimBulletPhysics.instance != null ) {
					MMD4MecanimBulletPhysics.instance.DebugLog();
				}
				Debug.LogError("");
				return false;
			}
		}
				
		private void _PrepareWork()
		{
			if( this.boneList == null ) {
				return;
			}

			int boneLength = this.boneList.Length;
			this._kinematicFlagsList		= new int[boneLength];
			this._kinematicTransformList	= new float[boneLength * 12];
			this._nonKinematicFlagsList		= new int[boneLength];
			this._nonKinematicTransformList	= new float[boneLength * 8];
		}
		
		public void Destroy()
		{	
			#if !NOUSE_BULLETXNA_UNITY
			if( this.bulletPMXModel != null ) {
				this.bulletPMXModel.Destroy();
				this.bulletPMXModel = null;
			}
			#endif
			if( this.mmdModelPtr != IntPtr.Zero ) {
				_DestroyMMDModel( this.mmdModelPtr );
				this.mmdModelPtr = IntPtr.Zero;
			}
			if( this.localWorld != null ) {
				this.localWorld.Destroy();
				this.localWorld = null;
			}
			this._scale				= Vector3.one;
			this._rScale			= Vector3.one;
			this._identityScale		= true;
			this.resetWorld			= false;
			this.physics			= null;
			this.boneList			= null;
			this.model				= null;
		}
		
		public void Update()
		{
			if( this._kinematicFlagsList		== null ||
				this._kinematicTransformList	== null ||
			   	this.boneList					== null ) {
				return;
			}

			float[] transformList = this._kinematicTransformList;
			for( int i = 0, f = 0; i < this.boneList.Length; ++i, f += 12 ) {
				Bone bone = this.boneList[i];
				if( bone != null ) {
					if( bone.gameObject != null && (!bone.isRigidBody || bone.isKinematic) ) {
						this._kinematicFlagsList[i] = 1;
						Matrix4x4 matrix = bone.gameObject.transform.localToWorldMatrix;
						if( !_identityScale ) {
							MMD4MecanimCommon.NormalizeMatrixBasis( ref matrix, ref _rScale );
						}
						transformList[f + 0] = matrix.m00;
						transformList[f + 1] = -matrix.m10;
						transformList[f + 2] = -matrix.m20;
						transformList[f + 3] = -matrix.m01;
						transformList[f + 4] = matrix.m11;
						transformList[f + 5] = matrix.m21;
						transformList[f + 6] = -matrix.m02;
						transformList[f + 7] = matrix.m12;
						transformList[f + 8] = matrix.m22;
						transformList[f + 9] = -matrix.m03;
						transformList[f + 10] = matrix.m13;
						transformList[f + 11] = matrix.m23;
					}
				}
			}

			#if !NOUSE_BULLETXNA_UNITY
			if( this.bulletPMXModel != null ) {
				this.bulletPMXModel.Update( this._kinematicFlagsList, this._kinematicTransformList );
				return;
			}
			#endif

			if( this.mmdModelPtr != IntPtr.Zero ) {
				GCHandle gch_kinematicFlagsList = GCHandle.Alloc( this._kinematicFlagsList, GCHandleType.Pinned );
				GCHandle gch_kinematicTransformList = GCHandle.Alloc( this._kinematicTransformList, GCHandleType.Pinned );
				IntPtr gch_kinematicFlagsAddr = gch_kinematicFlagsList.AddrOfPinnedObject();
				IntPtr gch_kinematicTransformAddr = gch_kinematicTransformList.AddrOfPinnedObject();

				_UpdateMMDModel( this.mmdModelPtr,
					gch_kinematicFlagsAddr, this._kinematicFlagsList.Length,
					gch_kinematicTransformAddr, this._kinematicTransformList.Length );

				gch_kinematicTransformList.Free();
				gch_kinematicFlagsList.Free();
			}
		}
		
		public void LateUpdate( float deltaTime )
		{
			if( this._nonKinematicFlagsList		== null ||
				this._nonKinematicTransformList	== null ||
				this.boneList					== null ) {
				return;
			}
			
			if( !this.resetWorld && this.localWorld != null ) {
				this.resetWorld = true;
				//MMD4MecanimBulletPhysicsResetWorldMMDModel( this.mmdModelPtr );
			}
			
			if( this.localWorld != null ) {
				this.localWorld.Update( deltaTime );
			}

			#if !NOUSE_BULLETXNA_UNITY
			if( this.bulletPMXModel != null ) {
				this.bulletPMXModel.LateUpdate( this._nonKinematicFlagsList, this._nonKinematicTransformList );
			}
			#endif

			if( this.mmdModelPtr != IntPtr.Zero ) {
				GCHandle gch_nonKinematicFlagsList = GCHandle.Alloc( this._nonKinematicFlagsList, GCHandleType.Pinned );
				GCHandle gch_nonKinematicTransformList = GCHandle.Alloc( this._nonKinematicTransformList, GCHandleType.Pinned );
				IntPtr gch_nonKinematicFlagsAddr = gch_nonKinematicFlagsList.AddrOfPinnedObject();
				IntPtr gch_nonKinematicTransformAddr = gch_nonKinematicTransformList.AddrOfPinnedObject();

				_LateUpdateMMDModel( this.mmdModelPtr,
					gch_nonKinematicFlagsAddr, this._nonKinematicFlagsList.Length,
					gch_nonKinematicTransformAddr, this._nonKinematicTransformList.Length );

				gch_nonKinematicTransformList.Free();
				gch_nonKinematicFlagsList.Free();
			}

			float[] transformList = this._nonKinematicTransformList;
			for( int i = 0, f = 0; i < this.boneList.Length; ++i, f += 8 ) {
				Bone bone = this.boneList[i];
				if( bone != null ) {
					if( bone.isRigidBody && !bone.isKinematic && bone.gameObject != null ) {
						Quaternion quaternion = new Quaternion(
							transformList[f + 4],
							-transformList[f + 5],
							-transformList[f + 6],
							transformList[f + 7] );
						
						bone.gameObject.transform.localRotation = quaternion;
						bone.gameObject.transform.localPosition = new Vector3( -transformList[f + 0], transformList[f + 1], transformList[f + 2] );
					}
				}
			}
		}

		public Bone SetKinematicBone( int boneID, bool isKinematic )
		{
			if( (uint)boneID < (uint)boneList.Length ) {
				boneList[boneID].isKinematic = isKinematic;
				
				PropertyWriter propertyWriter = new PropertyWriter();
				propertyWriter.Write( "isRigidBodyKinematic", isKinematic ? 1 : 0 );
				
				propertyWriter.Lock();
				_ConfigBoneMMDModel(
					this.mmdModelPtr, boneID,
					propertyWriter.iValuesPtr, propertyWriter.iValueLength,
					propertyWriter.fValuesPtr, propertyWriter.fValueLength );
				propertyWriter.Unlock();
				
				return boneList[boneID];
			}
			return null;
		}
	}
	
	void Awake()
	{
		if( _instance == null ) {
			_instance = this;
		} else {
			if( _instance != this ) {
				Destroy( this.gameObject );
				return;
			}
		}

		_Initialize();
	}
	
	void LateUpdate()
	{
		_InternalUpdate();
	}
	
	void _InternalUpdate()
	{
		if( _isAwaked ) {
			foreach( RigidBody rigidBody in _rigidBodyList ) {
				rigidBody.Update();
			}
			foreach( MMDModel mmdModel in _mmdModelList ) {
				mmdModel.Update();
			}

			World globalWorld = this.globalWorld;
			if( globalWorld != null ) {
				globalWorld.Update( Time.deltaTime );
			}
			
			foreach( RigidBody rigidBody in _rigidBodyList ) {
				rigidBody.LateUpdate();
			}
			foreach( MMDModel mmdModel in _mmdModelList ) {
				mmdModel.LateUpdate( Time.deltaTime );
			}

			DebugLog();
		}
	}

	void OnDestroy()
	{
		foreach( RigidBody rigidBody in _rigidBodyList ) {
			rigidBody.Destroy();
		}
		foreach( MMDModel mmdModel in _mmdModelList ) {
			mmdModel.Destroy();
		}
		_rigidBodyList.Clear();
		_mmdModelList.Clear();
		if( _globalWorld != null ) {
			_globalWorld.Destroy();
			_globalWorld = null;
		}
		if( _instance == this ) {
			_instance = null;
		}
	}

	IEnumerator DelayedAwake()
	{
		yield return new WaitForEndOfFrame();
		_isAwaked = true;
		yield break;
	}
	
	private void _ActivateGlobalWorld()
	{
		if( _globalWorld == null ) {
			_globalWorld = new World();
		}
		if( this.globalWorldProperty == null ) {
			this.globalWorldProperty = new WorldProperty();
		}
		if( _globalWorld.isExpired ) {
			_globalWorld.Create( this.globalWorldProperty );
		}
	}
	
	public void DebugLog()
	{
		#if !NOUSE_BULLETXNA_UNITY
		if( _isUseBulletXNA ) {
			return;
		}
		#endif
		IntPtr debugLogPtr = _DebugLog( 1 );
		if( debugLogPtr != IntPtr.Zero ) {
			Debug.Log( Marshal.PtrToStringUni( debugLogPtr ) );
		}
	}
	
	public MMDModel CreateMMDModel( MMD4MecanimModel model )
	{
		MMDModel mmdModel = new MMDModel();
		if( !mmdModel.Create( model ) ) {
			Debug.LogError( "CreateMMDModel: Failed " + model.gameObject.name );
			return null;
		}

		_mmdModelList.Add( mmdModel );
		return mmdModel;
	}
	
	public void DestroyMMDModel( MMDModel mmdModel )
	{
		for( int i = 0; i < _mmdModelList.Count; ++i ) {
			if( _mmdModelList[i] == mmdModel ) {
				mmdModel.Destroy();
				_mmdModelList.Remove( mmdModel );
				return;
			}
		}
	}
	
	public RigidBody CreateRigidBody( MMD4MecanimRigidBody rigidBody )
	{
		RigidBody r = new RigidBody();
		if( !r.Create( rigidBody ) ) {
			return null;
		}
		
		_rigidBodyList.Add( r );
		return r;
	}
	
	public void DestroyRigidBody( RigidBody rigidBody )
	{
		for( int i = 0; i < _rigidBodyList.Count; ++i ) {
			if( _rigidBodyList[i] == rigidBody ) {
				rigidBody.Destroy();
				_rigidBodyList.Remove( rigidBody );
				return;
			}
		}
	}

	static IntPtr _DebugLog( int clanupFlag )
	{
		return MMD4MecanimBulletPhysicsDebugLog( clanupFlag );
	}

	static IntPtr _CreateWorld( IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		return MMD4MecanimBulletPhysicsCreateWorld( iValues, iValueLength, fValues, fValueLength );
	}

	static void _DestroyWorld( IntPtr worldPtr )
	{
		MMD4MecanimBulletPhysicsDestroyWorld( worldPtr );
	}

	static void _UpdateWorld( IntPtr worldPtr, float deltaTime )
	{
		MMD4MecanimBulletPhysicsUpdateWorld( worldPtr, deltaTime );
	}
	
	static IntPtr _CreateMMDModel( IntPtr mmdModelBytes, int mmdModelLength, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		return MMD4MecanimBulletPhysicsCreateMMDModel( mmdModelBytes, mmdModelLength, iValues, iValueLength, fValues, fValueLength );
	}

	static void _DestroyMMDModel( IntPtr mmdModelPtr )
	{
		MMD4MecanimBulletPhysicsDestroyMMDModel( mmdModelPtr );
	}

	static void _JoinWorldMMDModel( IntPtr worldPtr, IntPtr mmdModelPtr )
	{
		MMD4MecanimBulletPhysicsJoinWorldMMDModel( worldPtr, mmdModelPtr );
	}

	static void _LeaveWorldMMDModel( IntPtr mmdModelPtr )
	{
		MMD4MecanimBulletPhysicsLeaveWorldMMDModel( mmdModelPtr );
	}

	static void _ResetWorldMMDModel( IntPtr mmdModelPtr )
	{
		MMD4MecanimBulletPhysicsResetWorldMMDModel( mmdModelPtr );
	}

	static void _UpdateMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		MMD4MecanimBulletPhysicsUpdateMMDModel( mmdModelPtr, iValues, iValueLength, fValues, fValueLength );
	}

	static void _LateUpdateMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		MMD4MecanimBulletPhysicsLateUpdateMMDModel( mmdModelPtr, iValues, iValueLength, fValues, fValueLength );
	}

	static void _ConfigBoneMMDModel( IntPtr mmdModelPtr, int boneID, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		MMD4MecanimBulletPhysicsConfigBoneMMDModel( mmdModelPtr, boneID, iValues, iValueLength, fValues, fValueLength );
	}

	static int _GetBoneTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		return MMD4MecanimBulletPhysicsGetBoneTransformMMDModel( mmdModelPtr, iValues, iValueLength, fValues, fValueLength );
	}

	static int _GetRigidBodyTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		return MMD4MecanimBulletPhysicsGetRigidBodyTransformMMDModel( mmdModelPtr, iValues, iValueLength, fValues, fValueLength );
	}

	static int _GetJointTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		return MMD4MecanimBulletPhysicsGetJointTransformMMDModel( mmdModelPtr, iValues, iValueLength, fValues, fValueLength );
	}

	static IntPtr _CreateRigidBody( IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		return MMD4MecanimBulletPhysicsCreateRigidBody( iValues, iValueLength, fValues, fValueLength );
	}

	static void _DestroyRigidBody( IntPtr rigidBodyPtr )
	{
		MMD4MecanimBulletPhysicsDestroyRigidBody( rigidBodyPtr );
	}

	static void _JoinWorldRigidBody( IntPtr worldPtr, IntPtr rigidBodyPtr )
	{
		MMD4MecanimBulletPhysicsJoinWorldRigidBody( worldPtr, rigidBodyPtr );
	}

	static void _LeaveWorldRigidBody( IntPtr rigidBodyPtr )
	{
		MMD4MecanimBulletPhysicsLeaveWorldRigidBody( rigidBodyPtr );
	}

	static void _ResetWorldRigidBody( IntPtr rigidBodyPtr )
	{
		MMD4MecanimBulletPhysicsResetWorldRigidBody( rigidBodyPtr );
	}

	static void _UpdateRigidBody( IntPtr rigidBodyPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		MMD4MecanimBulletPhysicsUpdateRigidBody( rigidBodyPtr, iValues, iValueLength, fValues, fValueLength );
	}

	static void _LateUpdateRigidBody( IntPtr rigidBodyPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength )
	{
		MMD4MecanimBulletPhysicsLateUpdateRigidBody( rigidBodyPtr, iValues, iValueLength, fValues, fValueLength );
	}

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern IntPtr MMD4MecanimBulletPhysicsDebugLog( int clanupFlag );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern IntPtr MMD4MecanimBulletPhysicsCreateWorld( IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsDestroyWorld( IntPtr worldPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsUpdateWorld( IntPtr worldPtr, float deltaTime );

	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern IntPtr MMD4MecanimBulletPhysicsCreateMMDModel( IntPtr mmdModelBytes, int mmdModelLength, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsDestroyMMDModel( IntPtr mmdModelPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsJoinWorldMMDModel( IntPtr worldPtr, IntPtr mmdModelPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsLeaveWorldMMDModel( IntPtr mmdModelPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsResetWorldMMDModel( IntPtr mmdModelPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsUpdateMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsLateUpdateMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsConfigBoneMMDModel( IntPtr mmdModelPtr, int boneID, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern int  MMD4MecanimBulletPhysicsGetBoneTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern int  MMD4MecanimBulletPhysicsGetRigidBodyTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern int  MMD4MecanimBulletPhysicsGetJointTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );

	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern IntPtr MMD4MecanimBulletPhysicsCreateRigidBody( IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsDestroyRigidBody( IntPtr rigidBodyPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsJoinWorldRigidBody( IntPtr worldPtr, IntPtr rigidBodyPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsLeaveWorldRigidBody( IntPtr rigidBodyPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsResetWorldRigidBody( IntPtr rigidBodyPtr );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsUpdateRigidBody( IntPtr rigidBodyPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
	[DllImport ("MMD4MecanimBulletPhysics")]
	public static extern void MMD4MecanimBulletPhysicsLateUpdateRigidBody( IntPtr rigidBodyPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength );
#else
	public static IntPtr MMD4MecanimBulletPhysicsDebugLog( int cleanupFlag ) { return IntPtr.Zero; }
	public static IntPtr MMD4MecanimBulletPhysicsCreateWorld( IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) { return IntPtr.Zero; }
	public static void MMD4MecanimBulletPhysicsDestroyWorld( IntPtr worldPtr ) {}
	public static void MMD4MecanimBulletPhysicsUpdateWorld( IntPtr worldPtr, float deltaTime ) {}

	public static IntPtr MMD4MecanimBulletPhysicsCreateMMDModel( IntPtr mmdModelBytes, int mmdModelLength, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) { return IntPtr.Zero; }
	public static void MMD4MecanimBulletPhysicsDestroyMMDModel( IntPtr mmdModelPtr ) {}
	public static void MMD4MecanimBulletPhysicsJoinWorldMMDModel( IntPtr worldPtr, IntPtr mmdModelPtr ) {}
	public static void MMD4MecanimBulletPhysicsLeaveWorldMMDModel( IntPtr mmdModelPtr ) {}
	public static void MMD4MecanimBulletPhysicsResetWorldMMDModel( IntPtr mmdModelPtr ) {}
	public static void MMD4MecanimBulletPhysicsUpdateMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) {}
	public static void MMD4MecanimBulletPhysicsLateUpdateMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) {}
	public static void MMD4MecanimBulletPhysicsConfigBoneMMDModel( IntPtr mmdModelPtr, int boneID, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) {}
	public static int  MMD4MecanimBulletPhysicsGetBoneTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) { return 0; }
	public static int  MMD4MecanimBulletPhysicsGetRigidBodyTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) { return 0; }
	public static int  MMD4MecanimBulletPhysicsGetJointTransformMMDModel( IntPtr mmdModelPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) { return 0; }

	public static IntPtr MMD4MecanimBulletPhysicsCreateRigidBody( IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) { return IntPtr.Zero; }
	public static void MMD4MecanimBulletPhysicsDestroyRigidBody( IntPtr rigidBodyPtr ) {}
	public static void MMD4MecanimBulletPhysicsJoinWorldRigidBody( IntPtr worldPtr, IntPtr rigidBodyPtr ) {}
	public static void MMD4MecanimBulletPhysicsLeaveWorldRigidBody( IntPtr rigidBodyPtr ) {}
	public static void MMD4MecanimBulletPhysicsResetWorldRigidBody( IntPtr rigidBodyPtr ) {}
	public static void MMD4MecanimBulletPhysicsUpdateRigidBody( IntPtr rigidBodyPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) {}
	public static void MMD4MecanimBulletPhysicsLateUpdateRigidBody( IntPtr rigidBodyPtr, IntPtr iValues, int iValueLength, IntPtr fValues, int fValueLength ) {}
#endif
}
