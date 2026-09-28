namespace Serenity.Application.Common;

public static class Ordenamiento
{
    /// <summary>True si <paramref name="ids"/> contiene exactamente los mismos elementos que <paramref name="existentes"/>, sin repetir.</summary>
    public static bool EsPermutacion(IReadOnlyList<long>? ids, IReadOnlyCollection<long> existentes)
        => ids is not null
           && ids.Count == existentes.Count
           && ids.Distinct().Count() == ids.Count
           && ids.All(existentes.Contains);
}

/// <summary>Cuerpo de los endpoints PUT .../orden.</summary>
public sealed record OrdenInput(IReadOnlyList<long> Ids);
