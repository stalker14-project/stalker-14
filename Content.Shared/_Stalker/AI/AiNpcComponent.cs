// Content.Shared/_Stalker/AI/AiNpcComponent.cs
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes; // Added for DataField
using Robust.Shared.ViewVariables; // Added for ViewVariables
using Robust.Shared.GameObjects; // Added for RegisterComponent

// Note: Ensure the namespace matches the file path if different from original
namespace Content.Shared._Stalker.AI
{
    // Note: Access should point to the *Shared* system if one exists and needs access,
    // otherwise remove or point to a relevant shared system.
    [RegisterComponent, NetworkedComponent, Access(typeof(SharedAiNpcSystem))]
    public sealed partial class AiNpcComponent : Component
    {
        /// <summary>
        /// Base personality prompt or instructions for the AI model.
        /// This might be networked if the client needs to know it for some reason,
        /// otherwise, it could potentially be server-only as well.
        /// </summary>
        [DataField("prompt"), ViewVariables(VVAccess.ReadWrite)]
        public string BasePrompt { get; private set; } = "You are a helpful NPC in a space station environment.";

        // ConversationHistory removed - This state is server-side only and caused networking/serialization errors.
        // It will be managed by the server-side AINPCSystem.

        /// <summary>
        /// Maximum number of messages (user + assistant) to keep in history.
        /// This is primarily used by the server-side AINPCSystem.
        /// It doesn't necessarily need to be networked unless a client system needs it.
        /// </summary>
        [DataField("maxHistory"), ViewVariables(VVAccess.ReadWrite)]
        public int MaxHistory { get; private set; } = 10; // Keep last 5 pairs (user+assistant)
    }
}
