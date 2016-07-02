using System.Reflection;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Plagued", "Wernesgruner", 0.1)]
    [Description("Everyone is infected.")]

    class Plagued : RustPlugin
    {
        private static int plagueRange = 20;
        private static int plagueIncreaseRate = 1000;
        private static int plagueDecreaseRate = 1000;
        private static int plagueMinAffinity = 10;
        private static int affinityIncRate = 10;
        private static int affinityDecRate = 1;
        private static int playerLayer;

        // Get the buffer size from the Vis class using relfection. It should always be 8ko, but it might change in the future
        private static readonly Collider[] colBuffer = (Collider[])typeof(Vis).GetField("colBuffer", (BindingFlags.Static | BindingFlags.NonPublic)).GetValue(null);

        //
        private Dictionary<ulong, PlayerState> playerStates;

        void OnServerInitialized()
        {
            // Set the layer that will be used in the radius search. We only want human players in this case
            playerLayer = LayerMask.GetMask("Player (Server)");

            // Reload the player states
            playerStates = new Dictionary<ulong, PlayerState>();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                playerStates.Add(player.userID, new PlayerState(player));
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            // Add the proximity detector to the player
            player.gameObject.AddComponent<ProximityDetector>();

            // Add the player to the player state list
            if (!playerStates.ContainsKey(player.userID))
            {
                // The player was loaded in the current game session
                playerStates.Add(player.userID, new PlayerState(player));
                Puts(player.displayName + " has been plagued!");
            } else
            {
                // The player was already loaded in memory
                Puts(player.displayName + " has returned!");
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            ProximityDetector proximityDetector = player.gameObject.GetComponent<ProximityDetector>();
            proximityDetector.disableProximityCheck();
            Puts(player.displayName + " is no longer watched!");
        }

        void OnRunPlayerMetabolism(PlayerMetabolism metabolism)
        {
            // 0 - 1000 -> Decreased Health Regen
            // 1000 - 2000 -> Increased hunger
            // 2000 - 3000 -> Increased thirst
            // 3000 - 4000 -> No Health Regen
            // 4000 - 5000 -> No comfort
            // 5000 - 6000 -> Increased Hunger 2
            // 6000 - 7000 -> Increased Thirst 2
            // 7000 - 8000 -> Cold
            // 8000 - 9000 -> Bleeding
            // 9000+ -> Poison

            /*
             * -- ----------------------------
             * -- Rust default rates
             * -- ----------------------------
             * -- healthgain = 0.03
             * -- caloriesloss = 0 - 0.05
             * -- hydrationloss = 0 - 0.025
             * -- ----------------------------
             */
            BasePlayer player = metabolism.GetComponent<BasePlayer>();
            PlayerState state = playerStates[player.userID];
            int plagueLevel = state.getPlagueLevel();
            float defaultHealthGain = 0.03f;
            float defaultCaloriesLoss = 0.05f;
            float defaultHydrationLoss = 0.025f;


            //Interface.Oxide.LogInfo("Infection stage " + (plagueLevel / 1000).ToString());

            if (plagueLevel == 0)
            {
                //Interface.Oxide.LogInfo("Uninfected");
                return;
            }
            if (plagueLevel >= 1)
            {
                //Interface.Oxide.LogInfo("Infection stage 1 " + player.displayName + " " + player.userID);
                metabolism.pending_health.value = metabolism.pending_health.value + (defaultHealthGain / 2f);
            }
            else
            {
                return;
            }
            if (plagueLevel >= 1000)
            {
                //Interface.Oxide.LogInfo("Infection stage 2");
                metabolism.calories.value = metabolism.calories.value - ((defaultCaloriesLoss * 3f) + (metabolism.heartrate.value / 10f));
            }
            else
            {
                return;
            }
            if (plagueLevel >= 2000)
            {
                //Interface.Oxide.LogInfo("Infection stage 3");
                metabolism.hydration.value = metabolism.hydration.value - ((defaultHydrationLoss * 3f) + (metabolism.heartrate.value / 10f));
            }
            else
            {
                return;
            }
            if (plagueLevel >= 3000)
            {
                //Interface.Oxide.LogInfo("Infection stage 4");
                metabolism.pending_health.value = metabolism.pending_health.value - (defaultHealthGain / 2f);
            }
            else
            {
                return;
            }
            if (plagueLevel >= 4000)
            {
                //Interface.Oxide.LogInfo("Infection stage 5");
                metabolism.comfort.value = -1;
            }
            else
            {
                return;
            }
            if (plagueLevel >= 5000)
            {
                //Interface.Oxide.LogInfo("Infection stage 6");
                metabolism.calories.value = metabolism.calories.value - ((defaultCaloriesLoss * 5f) + (metabolism.heartrate.value / 10f));
            }
            else
            {
                return;
            }
            if (plagueLevel >= 6000)
            {
                //Interface.Oxide.LogInfo("Infection stage 7");
                metabolism.hydration.value = metabolism.hydration.value - ((defaultHydrationLoss * 5f) + (metabolism.heartrate.value / 10f));
            }
            else
            {
                return;
            }
            if (plagueLevel >= 7000)
            {
                ///Interface.Oxide.LogInfo("Infection stage 8");
                metabolism.temperature.value = metabolism.temperature.value - 0.05f;
            }
            else
            {
                return;
            }
            if (plagueLevel >= 8000)
            {
                //Interface.Oxide.LogInfo("Infection stage 9");
                metabolism.bleeding.value = metabolism.bleeding.value + 0.005f;
            }
            else
            {
                return;
            }
            if (plagueLevel == 10000)
            {
                //Interface.Oxide.LogInfo("Infection stage 10");
                metabolism.poison.value = 2;
            }
        }

        void OnPlayerProximity(BasePlayer player, BasePlayer[] players)
        {
            if (playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID].increasePlaguePenalty(players);
                //Puts(player.displayName + " is close to " + (players.Length - 1).ToString() + " other players!");
            } else
            {
                Puts(player.displayName + " has no state!");
            }
        }

        void OnPlayerAlone(BasePlayer player)
        {
            if (playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID].decreasePlaguePenalty();
            }
            else
            {
                Puts(player.displayName + " has no state!");
            }
        }

        public class CustomMetabolism : PlayerMetabolism
        {
            public BasePlayer getOwner()
            {
                return this.owner;
            }
        }

        /**
         * This class handles the in-memory state of a player.
         */
        public class PlayerState
        {
            private BasePlayer player;
            private int plagueLevel;
            private Dictionary<ulong, int> associates;
            private Dictionary<ulong, int> kin;
            private bool pristine;

            /**
             * Retrieves a player from database and restore its store or creates a new database entry
             */
            public PlayerState(BasePlayer newPlayer)
            {
                player = newPlayer;
                plagueLevel = 0;
                associates = new Dictionary<ulong, int>();
            }

            /**
             * Increases the affinity of an associate and returns his new affinity
             */
            public int increaseAssociateAffinity(BasePlayer associate)
            {
                if (associate == null) return -1;
                if (player.userID == associate.userID) return -1;

                if (associates.ContainsKey(associate.userID))
                {
                    if ((associates[associate.userID] + affinityIncRate) < int.MaxValue) associates[associate.userID] += affinityIncRate;
                }
                else
                {
                    associates.Add(associate.userID, 0);
                }

                //Interface.Oxide.LogInfo(player.displayName + " -> " + associate.displayName + " = " + associates[associate.userID].ToString());

                return associates[associate.userID];
            }

            /**
             * Increases the affinity of all the associates in the list and increases the plague penalty if some associates are over the plague threshold
             * It also decreases the plague treshold if all the associates are kin or under the threshold
             */
            public void increasePlaguePenalty(BasePlayer[] associates)
            {
                int contagionVectorsCount = 0;

                foreach (BasePlayer associate in associates)
                {
                    int affinity = increaseAssociateAffinity(associate);

                    if (affinity >= plagueMinAffinity)
                    {
                        contagionVectorsCount++;
                    }
                }

                if (contagionVectorsCount > 0)
                {
                    increasePlagueLevel(contagionVectorsCount);
                } else
                {
                    decreasePlagueLevel();
                }
            }

            /**
             * Decreases the affinity of all associates and decreases the plague level.
             */
            public void decreasePlaguePenalty()
            {
                if (pristine) return;

                List<ulong> keys = new List<ulong>(associates.Keys);

                foreach (ulong key in keys)
                {
                    if ((associates[key] - affinityDecRate) >= 0)
                    {
                        associates[key] = associates[key] - affinityDecRate;
                    }
                }

                decreasePlagueLevel();
            }

            public void increasePlagueLevel(int contagionVectorCount)
            {
                if ((plagueLevel + (contagionVectorCount * plagueIncreaseRate)) <= 10000) {
                    plagueLevel += contagionVectorCount * plagueIncreaseRate;
                }

                //Interface.Oxide.LogInfo(player.displayName + "'s new plague level: " + plagueLevel.ToString());
            }

            public void decreasePlagueLevel()
            {
                if ((plagueLevel - plagueDecreaseRate) >= 0)
                {
                    plagueLevel -= plagueDecreaseRate;
                    //Interface.Oxide.LogInfo(player.displayName + "'s new plague level: " + plagueLevel.ToString());
                }
            }

            public int getPlagueLevel()
            {
                return plagueLevel;
            }
        }

        /**
         * This component adds a timers and collects all players colliders in a given radius. It then triggers custom hooks to reflect the situation of a given player
         */
        public class ProximityDetector : MonoBehaviour
        {
            public BasePlayer player;

            public void disableProximityCheck()
            {
                CancelInvoke("CheckProximity");
            }

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckProximity", 2f, 1.5f);
            }

            void OnDestroy()
            {
                disableProximityCheck();
            }

            void CheckProximity()
            {
                var count = Physics.OverlapSphereNonAlloc(player.transform.position, plagueRange, colBuffer, playerLayer);

                if (count > 1)
                {
                    BasePlayer[] playersNear = new BasePlayer[count];
                    for (int i = 0; i < count; i++)
                    {
                        var collider = colBuffer[i];
                        colBuffer[i] = null;
                        var collidingPlayer = collider.GetComponentInParent<BasePlayer>();
                        playersNear[i] = collidingPlayer;
                    }
                    notifyPlayerProximity(playersNear);
                } else
                {
                    notifyPlayerAlone();
                }
            }

            void notifyPlayerProximity(BasePlayer[] players)
            {
                Interface.Oxide.CallHook("OnPlayerProximity", player, players);
            }

            void notifyPlayerAlone()
            {
                Interface.Oxide.CallHook("OnPlayerAlone", player);
            }
        }
    }
}