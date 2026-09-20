//implemented by every virus's attack script, so one animator driver can serve all of them
//instead of each virus type needing its own.
public interface IVirusAttack
{
    //raised when a blow actually lands. the number is which swing animation to play, for
    //models that have more than one.
    event System.Action<int> OnAttack;
}
