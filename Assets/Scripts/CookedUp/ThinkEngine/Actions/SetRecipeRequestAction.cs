using CookedUp.Core;
using CookedUp.Core.Players;
using ThinkEngine.Planning;
using UnityEngine;

namespace CookedUp.ThinkEngine.Actions
{
    class SetRecipeRequestAction : Action, IPlayerAction
    {
        private IDManager idManager;
        private DeliveryManager deliveryManager;
        
        public int PlayerID { get; set; }
        
        public int RecipeRequestID { get; set; }

        private Player player;
        private PlayerBot playerBot;
        
        private RecipeRequest recipeRequest;


        public override void Init() {

            Debug.Log($"[{GetType().Name}]: Init, PlayerID: {PlayerID}, RecipeRequestID: {RecipeRequestID}");
            idManager = IDManager.Instance;
            deliveryManager =  DeliveryManager.Instance;
            recipeRequest = deliveryManager.GetRecipeRequestFromID(RecipeRequestID);
            
            player = idManager.GetComponentFromID<Player>(PlayerID);
            playerBot = player.GetComponent<PlayerBot>();
        }

        public override State Prerequisite() {
            return State.READY;
        }

        public override void Do() {
            
            playerBot.SetRecipeRequest(recipeRequest);
            
            if(playerBot.CurrentRecipeRequest != recipeRequest)
                Debug.Log($"[{GetType().Name}]: {player.name} failed to set recipe request to {recipeRequest.Recipe.name}");
        }

        public override State Done() {
            return State.READY;
        }

        public override void Clean() {
            Debug.Log($"[{GetType().Name}]: Clean, PlayerID: {PlayerID}, RecipeRequestID: {RecipeRequestID}");
        }
    }
}
