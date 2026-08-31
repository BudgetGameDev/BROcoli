namespace BudgetGameDev.Games.Brocoli
{
    public partial class LevelUpScreen
    {
        internal void ProcessKeyboardShortcuts(bool first, bool second, bool third)
        {
            if (first)
                ChooseUpgrade(0);
            else if (second)
                ChooseUpgrade(1);
            else if (third)
                ChooseUpgrade(2);
        }
    }
}
