using UnityEngine;
using UnityEngine.InputSystem;

//sits on the player: hotkeys that order every squad
public class SquadCommander : MonoBehaviour
{
    public Key seekOutKey = Key.Digit1;
    public Key followKey = Key.Digit2;
    public Key holdKey = Key.Digit3;
    [Tooltip("Forms the surround round the player; pressed again, it breaks up.")]
    public Key surroundKey = Key.Digit4;
    public Key formationKey = Key.B;

    [Tooltip("Seconds the order stays on screen.")]
    public float toastTime = 1.5f;

    SquadFormation formation = SquadFormation.Wedge;
    string toast;
    float toastUntil;

    //added to the player at runtime if the scene doesn't already have one
    public static void Ensure()
    {
        if (FindFirstObjectByType<SquadCommander>() != null) return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.gameObject.AddComponent<SquadCommander>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || Time.timeScale == 0f) return;

        if (kb[seekOutKey].wasPressedThisFrame) Order(SquadOrder.SeekOut, "Seek out viruses");
        if (kb[holdKey].wasPressedThisFrame) Order(SquadOrder.HoldPosition, "Hold position");
        if (kb[followKey].wasPressedThisFrame) Order(SquadOrder.FollowPlayer, "Follow me");
        if (kb[surroundKey].wasPressedThisFrame) ToggleSurround();

        if (kb[formationKey].wasPressedThisFrame)
        {
            formation = (SquadFormation)(((int)formation + 1) % System.Enum.GetValues(typeof(SquadFormation)).Length);
            foreach (Squad s in Squad.All) s.SetFormation(formation);
            Show("Formation: " + formation);
        }
    }

    void Order(SquadOrder order, string label)
    {
        foreach (Squad s in Squad.All) s.Command(order, transform);
        Show(label);
    }

    void ToggleSurround()
    {
        bool up = false;
        foreach (Squad s in Squad.All) up |= s.Surrounding;

        if (up)
        {
            foreach (Squad s in Squad.All)
                if (s.Surrounding) s.BreakSurround(transform);
            Show("Break formation");
            return;
        }

        Order(SquadOrder.Surround, "Surround me");
    }

    void Show(string text)
    {
        toast = "Squad: " + text;
        toastUntil = Time.unscaledTime + toastTime;
    }

    void OnGUI()
    {
        if (Time.unscaledTime >= toastUntil) return;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(0.4f, 1f, 1f);
        GUI.Label(new Rect(0f, Screen.height * 0.18f, Screen.width, 40f), toast, style);
    }
}
