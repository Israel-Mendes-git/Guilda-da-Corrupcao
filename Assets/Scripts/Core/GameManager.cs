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

    // Chamado quando a jornada termina.
    public void OnJourneyComplete(bool success, int reward)
    {
        if (!success) return;

        GuildManager.Instance.AddGold(reward);

        // Atualiza os recrutas da taverna após a jornada.
        if (tavernManager != null)
            tavernManager.RefreshRecruits();
    }
}
