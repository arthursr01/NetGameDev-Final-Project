using System.Net.WebSockets;
using Unity.Netcode;

using Unity.VisualScripting;
using UnityEngine;


public class Player : NetworkBehaviour
{


    public NetworkVariable<Vector3> PositionChange = new NetworkVariable<Vector3>();
    public NetworkVariable<Vector3> RotationChange = new NetworkVariable<Vector3>();
    public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>(Color.red);
    public NetworkVariable<int> Score = new NetworkVariable<int>(50);
    

    public TMPro.TMP_Text txtScoreDisplay;
    public TMPro.TMP_Text txtTimeDisplay;


    private GameManager _gameMgr;
    private Camera _camera;
    public float movementSpeed = .5f;
    public float rotationSpeed = 1f;
    private BulletSpawner _bulletSpawner;
    public float timeRemaining = 30f;
    public Vector3 startPos;
    public float maxDist = 10;
    public float currentDist;
    


    private void Start()
    {
        ApplyPlayerColor();
        PlayerColor.OnValueChanged += OnPlayerColorChanged;
        startPos = transform.position;

    }

    public override void OnNetworkSpawn()
    {
        _camera = transform.Find("Camera").GetComponent<Camera>();
        _camera.enabled = IsOwner;

        Score.OnValueChanged += ClientOnScoreChanged;
        _bulletSpawner = transform.Find("RArm").transform.Find("BulletSpawner").GetComponent<BulletSpawner>();
        
        if (IsHost)
        {
            _bulletSpawner.BulletDamage.Value = 1;
        }
        DisplayScore();
        RequestSetScoreServerRpc(50);
        
    }

    private void HostHandleBulletCollision(GameObject bullet)
    {
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        Score.Value -= bulletScript.BulletDamage.Value;

        ulong ownerClientID = bullet.GetComponent<NetworkObject>().OwnerClientId;
        Player otherPlayer = NetworkManager.Singleton.ConnectedClients[ownerClientID].PlayerObject.GetComponent<Player>();
        otherPlayer.Score.Value += bulletScript.BulletDamage.Value;

        Destroy(bullet);
    }

    private void HostHandleDamageBoostPickup(Collider other)
    {
        if (!_bulletSpawner.IsAtMaxDamage())
        {
            _bulletSpawner.IncreaseDamage();
            other.GetComponent<NetworkObject>().Despawn();
        }
    }

    private void ClientOnScoreChanged(int previous, int current)
    {
        DisplayScore();
    }

    [ServerRpc]
    void RequestPositionForMovementServerRpc(Vector3 posChange, Vector3 rotChange)
    {
        if (!IsServer && !IsHost) return;

        PositionChange.Value = posChange;
        RotationChange.Value = rotChange;
    }

    [ServerRpc]
    public void RequestSetScoreServerRpc(int value)
    {
        Score.Value = value;
    }

   

    public void OnPlayerColorChanged(Color previous, Color current)
    {
        ApplyPlayerColor();
    }



    public void OnCollisionEnter(Collision collision)
    {
        if (IsHost)
        {
            if (collision.gameObject.CompareTag("Bullet"))
            {
                HostHandleBulletCollision(collision.gameObject);
            }
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (IsHost)
        {
            if (other.gameObject.CompareTag("DamageBoost"))
            {
                HostHandleDamageBoostPickup(other);
            }
        }
    }

    public void ApplyPlayerColor()
    {
        GetComponent<MeshRenderer>().material.color = PlayerColor.Value;
        transform.Find("Body").GetComponent<MeshRenderer>().material.color = PlayerColor.Value;
        transform.Find("RArm").GetComponent<MeshRenderer>().material.color = PlayerColor.Value;
        //transform.Find("LArm").GetComponent<MeshRenderer>().material.color = PlayerColor.Value;
        transform.Find("LTred").GetComponent<MeshRenderer>().material.color = PlayerColor.Value;
        transform.Find("RTred").GetComponent<MeshRenderer>().material.color = PlayerColor.Value;
        //transform.Find("RArmPiece").GetComponent<MeshRenderer>().material.color = PlayerColor.Value;
    }


    // horiz changes y rotation or x movement if shift down, vertical moves forward and back.
    private Vector3[] CalcMovement()
    {
        bool isShiftKeyDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float x_move = 0.0f;
        float z_move = Input.GetAxis("Vertical");
        float y_rot = 0.0f;

        if (isShiftKeyDown)
        {
            x_move = Input.GetAxis("Horizontal");
        }
        else
        {
            y_rot = Input.GetAxis("Horizontal");
        }

        Vector3 moveVect = new Vector3(x_move, 0, z_move);
        moveVect *= movementSpeed;

        Vector3 rotVect = new Vector3(0, y_rot, 0);
        rotVect *= rotationSpeed;

        return new[] { moveVect, rotVect };
    }


    void Update()
    {
        currentDist = Vector3.Distance(startPos, transform.position);
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            txtTimeDisplay.text = timeRemaining.ToString().Substring(0, 3);
        }

        else if (timeRemaining <= 0)
        {
            PauseGame();
            
        }
        txtTimeDisplay.text = timeRemaining.ToString().Substring(0, 3);
        if (IsOwner)
        {
            Vector3[] results = CalcMovement();
            RequestPositionForMovementServerRpc(results[0], results[1]);
            if (Input.GetButtonDown("Fire1"))
            {
                _bulletSpawner.FireServerRpc();
            }
        }

        if (!IsOwner || IsHost)
        {
            transform.Translate(PositionChange.Value);
            transform.Rotate(RotationChange.Value);
        }

        if (currentDist >= maxDist)
        {
            transform.position = startPos;
        }

        if (Score.Value <= 0)
        {
            var scene = NetworkManager.SceneManager.LoadScene("GameOver",
        UnityEngine.SceneManagement.LoadSceneMode.Single);
            PauseGame();
            
        }

    }

    public void DisplayScore()
    {
        txtScoreDisplay.text = Score.Value.ToString();
    }

    public void PauseGame()
    {
        Time.timeScale = 0;
    }
}