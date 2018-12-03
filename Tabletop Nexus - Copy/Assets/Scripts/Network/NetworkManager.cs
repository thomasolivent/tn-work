using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vuforia;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class NetworkManager : Photon.PunBehaviour {

    [SerializeField]
    private DungeonSaveLoad dungeonLoader;
    [SerializeField]
    DungeonSaveLoad dsl;
    [SerializeField]
    CharacterCreator cc;
    public List<Vector3> dungeonData = new List<Vector3>(); 
    public int roomTargets;
    Hashtable hash2;

    private enum NetworkMessage 
    {
        SpawnDungeon
    }

    void Awake()
    {
        PhotonNetwork.autoJoinLobby = false;
        // send rate is how many updates per second a PhotonView should send
        // default is 10
        PhotonNetwork.sendRate = 10;
        PhotonNetwork.sendRateOnSerialize = 10;
        PhotonNetwork.automaticallySyncScene = true;
        PhotonNetwork.OnEventCall += OnNetworkMessage;
    }

    public void Connect()
    {
        PhotonNetwork.ConnectUsingSettings("0.1"); //Version Compatibility
        //print("Connecting...");
    }

    public override void OnConnectedToMaster()
    {
        //print("Connected!");
    }

    public override void OnCreatedRoom()
    {
        dungeonData = new List<Vector3>();

        roomTargets = dsl.udtEventHandler.targets.Count;
        Hashtable hash = new Hashtable();
        hash.Add("TargetsCount", roomTargets);
        PhotonNetwork.room.SetCustomProperties(hash);

        //wip stuff
        hash2 = new Hashtable();
        foreach(var target in dsl.udtEventHandler.targets)
        {
            foreach (Transform child in target.transform)
            {
                if (child.tag == "Dungeon")
                {
                    TileScript[] allTiles = GetComponentsInChildren<TileScript>();
                    List<Vector3> tilePositions = new List<Vector3>();

                    foreach (TileScript t in allTiles)
                    {
                        tilePositions.Add(t.gameObject.transform.position);
                    }

                    hash2.Add(target.gameObject.name, tilePositions);
                }
            }
        }
    }

    public override void OnPhotonPlayerConnected(PhotonPlayer player)
    {
        // dungeons are synced with new players when they connect
        if (PhotonNetwork.isMasterClient && hash2.Count > 0)
        {
            RaiseEventOptions receivers = new RaiseEventOptions();
            receivers.TargetActors = new int[] {player.ID};
            if (hash2.Count == 0)
            {
                // load dungeon from local file
                SpawnDungeonOverNetwork(dungeonLoader.GetDungeonData(), receivers);
            }
            else
            {
                // if dungeon data has been stored, use that instead
                // i.e. if the original master client has left
                SpawnDungeonOverNetwork(dungeonData, receivers);
            }
        }
    }

    public override void OnJoinedRoom()
    {
        if (!PhotonNetwork.isMasterClient)
        {
            dsl.RemoveDungeonFromPlay();
            cc.RemoveCharacterFromPlay();

            if (!PhotonNetwork.isMasterClient)
            {
                roomTargets = (int)PhotonNetwork.room.CustomProperties["TargetsCount"];     //Should be able to destroy this if block and call SettingRoomTargets from OnPhotonPlayerConnected and pass through the hashtable created in OnCreatedRoom
                dsl.udtEventHandler.SettingRoomTargets(roomTargets);
            }
        }
    }

    public override void OnPhotonPlayerDisconnected(PhotonPlayer player)
    {
        // called when other player disconnects
        //ownership moves to room owner
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        // called when master client switches after original master client disconnects
    }

    public override void OnPhotonJoinRoomFailed(object[] codeAndMsg)
    {
        // called when player fails to join a room
        Debug.Log("Failed to join room!");
    }

    public override void OnConnectionFail(DisconnectCause cause)
    {
        // called when player fails to connect or disconnects from Photon servers
        Debug.Log("Disconnected!");
    }

    void OnNetworkMessage(byte messageId, object msg, int senderId)
    {
        if (messageId == (int)NetworkMessage.SpawnDungeon) {
            // sends dungeon spawn message to one or all players
            OnSpawnDungeon(msg, senderId);
        }
    }

    public void SpawnDungeonOverNetwork(List<Vector3> tileSpawnPositions, RaiseEventOptions receivers)
    {
        object[] msg = new object[tileSpawnPositions.Count];
        for (int i = 0; i < tileSpawnPositions.Count; i++)
        {
            msg[i] = tileSpawnPositions[i];
        }
        PhotonNetwork.RaiseEvent((int)NetworkMessage.SpawnDungeon, msg, true, receivers);
    }

    void OnSpawnDungeon(object msg, int senderId)
    {
        object[] receivedMsg = (object[])msg;
        List<Vector3> tileSpawnPositions = new List<Vector3>();
        for (int i = 0; i < receivedMsg.Length; i++)
        {
            tileSpawnPositions.Add((Vector3)receivedMsg[i]);
        }
        dungeonLoader.BuildDungeon(tileSpawnPositions);
        // keep track of dungeon data in case original master client leaves
        dungeonData = tileSpawnPositions;
    }

    void KickPlayer(string name)
    {
        // basic kick function - kicks player with given name
        // only master client can do this
        foreach (PhotonPlayer player in PhotonNetwork.playerList)
        {
            if (player.NickName == name)
            {
                PhotonNetwork.CloseConnection(player);
                return;
            }
        }
    }
    
}