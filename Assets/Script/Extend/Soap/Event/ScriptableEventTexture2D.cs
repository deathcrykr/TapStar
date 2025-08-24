using UnityEngine;
using Obvious.Soap;

[CreateAssetMenu(fileName = "scriptable_event_" + nameof(Texture2D), menuName = "Soap/ScriptableEvents/" + nameof(Texture2D))]
public class ScriptableEventTexture2D : ScriptableEvent<Texture2D>
{

}
