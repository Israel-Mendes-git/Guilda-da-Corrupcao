using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("References")]
    public TavernManager tavernManager;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // Chame este método quando a jornada terminar
    public void OnJourneyComplete(bool success, int reward)
    {
        if (success)
        {
            GuildManager.Instance.AddGold(reward);

            // ATUALIZA OS RECRUTAS DA TAVERNA APÓS A JORNADA
            if (tavernManager != null)
            {
                tavernManager.RefreshRecruits();
            }

        }
    }
}