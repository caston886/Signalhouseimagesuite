using SignalHouse.Core.Models;

namespace SignalHouse.Core.History;

/// <summary>
/// Snapshot-based undo/redo over an EditState. Pressing Tab in any editor
/// calls <see cref="Commit"/>, which pushes the current adjustment values as
/// a new, permanent history entry -- the same idea as pressing Enter to
/// commit a transform in other editors -- so the user can keep adjusting
/// from a known-good point and step backward through their edit session
/// afterward with Undo/Redo.
/// </summary>
public sealed class EditHistory
{
    private readonly List<EditState> _snapshots = new();
    private int _index = -1;

    public EditHistory(EditState initial)
    {
        Commit(initial);
    }

    public EditState Current => _snapshots[_index];

    public bool CanUndo => _index > 0;

    public bool CanRedo => _index < _snapshots.Count - 1;

    /// <summary>Pushes a new committed state, discarding any redo branch beyond the current point.</summary>
    public void Commit(EditState state)
    {
        if (_index < _snapshots.Count - 1)
        {
            _snapshots.RemoveRange(_index + 1, _snapshots.Count - _index - 1);
        }

        _snapshots.Add(state.Clone());
        _index = _snapshots.Count - 1;
    }

    public EditState Undo()
    {
        if (CanUndo)
        {
            _index--;
        }

        return Current;
    }

    public EditState Redo()
    {
        if (CanRedo)
        {
            _index++;
        }

        return Current;
    }
}
