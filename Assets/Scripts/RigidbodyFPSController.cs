﻿using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]

/**
 * This class contains player movement and health logic
 */
public class RigidbodyFPSController : MonoBehaviour
{

    public float speed = 10.0f;
    public float gravity = 9.8f;
    public float maxVelocityChange = 10.0f;
    public bool canJump = true;
    public float jumpHeight = 2.0f;
    private bool grounded = false;

    public int health;
    public int maxHealth = 100;

    public GameObject fpsCam;
    public GameObject me;
    public GameObject graphics; // tp graphics
    public GameObject ragDoll;
    public PhotonView playerStatusReceiver;

    // fp animations
    public AnimationManager fpAnimationManager;
    public AnimationClip fpWalk;
    public AnimationClip fpIdle;

    // tp animations
    public TpAnimationManager tpAnimationManager;
    public AnimationClip tpIdle;
    public AnimationClip tpRun;
    // TODO: add more tp animations here...

    // TODO: put the score menu in a better place
    public bool isPause = false;

    // TODO: remove this temp var
    private string dmgFrom = "";

    void Awake()
    {
        GetComponent<Rigidbody>().freezeRotation = true;
        GetComponent<Rigidbody>().useGravity = false;
        health = maxHealth;
        playerStatusReceiver.RPC("updateName", PhotonTargets.AllBuffered, PhotonNetwork.playerName);
        playerStatusReceiver.RPC("updateHP", PhotonTargets.AllBuffered, health, maxHealth);
    }

    void FixedUpdate()
    {
        if (grounded)
        {
            // Calculate how fast we should be moving
            Vector3 targetVelocity = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            targetVelocity = transform.TransformDirection(targetVelocity);
            targetVelocity *= speed;

            // Apply a force that attempts to reach our target velocity
            Vector3 velocity = GetComponent<Rigidbody>().velocity;
            Vector3 velocityChange = (targetVelocity - velocity);
            velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
            velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
            velocityChange.y = 0;
            GetComponent<Rigidbody>().AddForce(velocityChange, ForceMode.VelocityChange);

            // Jump
            if (canJump && Input.GetButton("Jump"))
            {
                GetComponent<Rigidbody>().velocity = new Vector3(velocity.x, CalculateJumpVerticalSpeed(), velocity.z);
            }

            // Play movement animation
            if (velocity.magnitude >= 0.1)
            {
                fpAnimationManager.playAnimation(fpWalk);
                // TODO: this is a temporary fix for idle is not animating after gun shot
                tpAnimationManager.stopAnimation();
                tpAnimationManager.playAnimation(tpRun);
            } else
            {
                fpAnimationManager.playAnimation(fpIdle);
                tpAnimationManager.stopPlay(tpRun);

                // Right now, tpIdle has conflicts with other tp animations
                // Without this check, tpIdle will overwrite everthing else
                if (tpAnimationManager.canPlay(tpIdle)) {
                    tpAnimationManager.playAnimation(tpIdle);
                }
                
            }
        }

        // We apply gravity manually for more tuning control
        GetComponent<Rigidbody>().AddForce(new Vector3(0, -gravity * GetComponent<Rigidbody>().mass, 0));

        grounded = false;

        if (health <= 0)
        {
            die();
        }

        // Hold SHIFT will pause the game
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            isPause = true;
        } 
        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            isPause = false;
        }
    }

    void OnCollisionStay()
    {
        grounded = true;
    }

    float CalculateJumpVerticalSpeed()
    {
        // From the jump height and gravity we deduce the upwards speed 
        // for the character to reach at the apex.
        return Mathf.Sqrt(2 * jumpHeight * gravity);
    }

    /*
     * Renders current player's hp and score
     */
    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 100, 30), "HP | " + health + "/" + maxHealth);
        GUI.Box(new Rect(10, 45, 100, 30), "Score | " + PhotonNetwork.player.GetScore());

        // Renders a score menu
        if (isPause)
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 150, 300, 300));
            GUILayout.Label("Scoreboard");
            foreach (PhotonPlayer player in PhotonNetwork.playerList)
            {
                GUILayout.Box(player.name + "                             " + player.GetScore());
            }
            GUILayout.EndArea();
        }
    }

    [PunRPC]
    public void applyDamage(int dmg, string fromPlayer)
    {
        dmgFrom = fromPlayer;
        int updatedHealth = 0;
        if (health - dmg <= 0)
        {
            // The displayed HP is never gonna get below 0
            updatedHealth = 0;

        } else
        {
            updatedHealth = health - dmg;
        }

        health -= dmg;
        // TODO: this should not be called in the RPC, should be called in local client
        if (GetComponent<PhotonView>().isMine)
        {
            playerStatusReceiver.RPC("updateHP", PhotonTargets.AllBuffered, updatedHealth, maxHealth);
            GameObject.Find("_NETWORK").GetComponent<FeedManager>().addDamageFeed(PhotonNetwork.playerName, dmgFrom, dmg);
        }
    }

    // called locally
    private void die()
    {
        // just a safety guard to make sure die is called only once when a player's hp <= 0
        health = maxHealth;

        // create a rag doll (dead body)
        Vector3 currentPosn = transform.position;
        GameObject doll = PhotonNetwork.Instantiate(ragDoll.name, new Vector3(currentPosn.x, currentPosn.y, currentPosn.z), transform.rotation, 0) as GameObject;

        // add kill feed
        GameObject.Find("_NETWORK").GetComponent<FeedManager>().addKillFeed(PhotonNetwork.playerName, dmgFrom);

        PhotonNetwork.Destroy(me);

        // back to the room menu
        GameObject.Find("_ROOM").GetComponent<RoomManager>().onDie(doll);
    }

}
