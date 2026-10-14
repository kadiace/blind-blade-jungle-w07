using System.Collections.Generic;

using UnityEngine;

/// <summary>한 방어 세션의 투척 예약과 비행을 집계하고 수리검 종료 구독을 소유한다.</summary>
internal sealed class DefenseThrowBudget
{
    private readonly HashSet<DefenseShuriken> _flights = new HashSet<DefenseShuriken>();
    private int _reserved;
    private float _nextAllowedAt;

    /// <summary>now, maximum, gap으로 투척 한도를 검사하고 성공 시 예약 수와 다음 허가 시각을 변경한다.</summary>
    internal bool TryReserve(float now, int maximum, float gap)
    {
        if (_flights.Count + _reserved >= maximum || now < _nextAllowedAt)
            return false;
        _reserved++;
        _nextAllowedAt = now + gap;
        return true;
    }

    /// <summary>보유 예약 하나를 반환하며 예약 수는 0 아래로 내리지 않는다.</summary>
    internal void CancelReservation()
    {
        _reserved = Mathf.Max(0, _reserved - 1);
    }

    /// <summary>shuriken의 예약을 비행으로 바꾸고 종료 구독을 한 번 등록한다.</summary>
    internal void Launch(DefenseShuriken shuriken)
    {
        CancelReservation();
        if (_flights.Add(shuriken))
            shuriken.Released += HandleReleased;
    }

    /// <summary>종료한 shuriken의 구독과 비행 집계를 해제한다.</summary>
    private void HandleReleased(DefenseShuriken shuriken)
    {
        shuriken.Released -= HandleReleased;
        _flights.Remove(shuriken);
    }

    /// <summary>세션 종료 시 비행 구독과 예약·다음 허가 시각을 정리한다. 투사체 자체는 파괴하지 않는다.</summary>
    internal void Clear()
    {
        foreach (DefenseShuriken shuriken in _flights)
            if (shuriken != null)
                shuriken.Released -= HandleReleased;
        _flights.Clear();
        _reserved = 0;
        _nextAllowedAt = 0f;
    }
}
