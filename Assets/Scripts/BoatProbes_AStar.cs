// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// Shout out to @holdingjason who posted a first version of this script here: https://github.com/huwb/crest-oceanrender/pull/100

using Crest.Internal;
using UnityEngine;

namespace Crest
{
    /// <summary>
    /// Boat physics for A* pathfinding system.
    /// Uses multiplicative AI input scaling.
    /// </summary>
    [AddComponentMenu(Internal.Constants.MENU_PREFIX_SCRIPTS + "Boat Probes AStar")]
    public class BoatProbes_AStar : BoatProbesBase
    {
        /// <summary>
        /// Applies AI forward input using multiplication.
        /// </summary>
        protected override float ApplyAIForwardInput(float currentForward)
        {
            return currentForward * AI_ForwardInput;
        }

        /// <summary>
        /// Applies AI turn input using multiplication.
        /// </summary>
        protected override float ApplyAITurnInput(float currentTurn)
        {
            return currentTurn * AI_TurnInput;
        }
    }
}
