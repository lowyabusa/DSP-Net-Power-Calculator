namespace DspNetEnergy.Core;

internal sealed record PlanConstraint(decimal[] Coefficients, decimal Limit);

/// <summary>Two-phase simplex for nonnegative activity rates and upper-bound constraints.</summary>
internal sealed class LinearPlan
{
    internal const decimal Tolerance = 0.000000000000000001m;
    private readonly decimal[,] tableau;
    private readonly int[] basic;
    private readonly int[] nonbasic;
    private readonly int constraintCount;
    private readonly int activityCount;
    private int RightHandSide => activityCount + 1;

    internal LinearPlan(IReadOnlyList<PlanConstraint> constraints, int activityCount)
    {
        this.activityCount = activityCount;
        constraintCount = constraints.Count;
        tableau = new decimal[constraintCount + 2, activityCount + 2];
        basic = new int[constraintCount];
        nonbasic = new int[activityCount + 1];

        for (int row = 0; row < constraintCount; row++)
        {
            for (int column = 0; column < activityCount; column++)
            {
                tableau[row, column] = constraints[row].Coefficients[column];
            }

            basic[row] = activityCount + row;
            tableau[row, activityCount] = -1m;
            tableau[row, RightHandSide] = constraints[row].Limit;
        }

        for (int column = 0; column < activityCount; column++)
        {
            nonbasic[column] = column;
            tableau[constraintCount, column] = 1m;
        }

        // The auxiliary variable relaxes every constraint during the feasibility pass.
        nonbasic[activityCount] = -1;
        tableau[constraintCount + 1, activityCount] = 1m;
    }

    internal decimal[]? Solve()
    {
        int lowest = 0;
        for (int row = 1; row < constraintCount; row++)
        {
            if (tableau[row, RightHandSide] < tableau[lowest, RightHandSide])
            {
                lowest = row;
            }
        }

        if (tableau[lowest, RightHandSide] < -Tolerance)
        {
            Pivot(lowest, activityCount);
            if (!Optimize(constraintCount + 1) || tableau[constraintCount + 1, RightHandSide] < -Tolerance)
            {
                return null;
            }

            RemoveAuxiliaryFromBasis();
        }

        // Minimize total activity over the selected recipes. Each unnecessary run has positive cost.
        if (!Optimize(constraintCount))
        {
            throw new InvalidOperationException("The production plan has an unbounded objective.");
        }

        var result = new decimal[activityCount];
        for (int row = 0; row < constraintCount; row++)
        {
            if (basic[row] >= 0 && basic[row] < activityCount)
            {
                result[basic[row]] = decimal.Round(Math.Max(0m, tableau[row, RightHandSide]), 18);
            }
        }

        return result;
    }

    private void RemoveAuxiliaryFromBasis()
    {
        int row = Array.IndexOf(basic, -1);
        if (row < 0)
        {
            return;
        }

        for (int column = 0; column <= activityCount; column++)
        {
            if (Math.Abs(tableau[row, column]) > Tolerance)
            {
                Pivot(row, column);
                return;
            }
        }
    }

    private bool Optimize(int objective)
    {
        while (true)
        {
            int entering = -1;
            for (int column = 0; column <= activityCount; column++)
            {
                if (nonbasic[column] >= 0 && tableau[objective, column] < -Tolerance &&
                    (entering < 0 || nonbasic[column] < nonbasic[entering]))
                {
                    entering = column;
                }
            }

            if (entering < 0)
            {
                return true;
            }

            int leaving = -1;
            decimal bestRatio = 0m;
            for (int row = 0; row < constraintCount; row++)
            {
                decimal coefficient = tableau[row, entering];
                if (coefficient <= Tolerance)
                {
                    continue;
                }

                decimal ratio = Math.Max(0m, tableau[row, RightHandSide]) / coefficient;
                if (leaving < 0 || ratio < bestRatio - Tolerance ||
                    Math.Abs(ratio - bestRatio) <= Tolerance && basic[row] < basic[leaving])
                {
                    leaving = row;
                    bestRatio = ratio;
                }
            }

            if (leaving < 0)
            {
                return false;
            }

            Pivot(leaving, entering);
        }
    }

    private void Pivot(int row, int column)
    {
        decimal pivot = tableau[row, column];
        for (int other = 0; other < constraintCount + 2; other++)
        {
            if (other == row || tableau[other, column] == 0m)
            {
                continue;
            }

            decimal scale = tableau[other, column] / pivot;
            for (int index = 0; index <= RightHandSide; index++)
            {
                if (index != column)
                {
                    tableau[other, index] -= tableau[row, index] * scale;
                }
            }

            tableau[other, column] = -scale;
        }

        for (int index = 0; index <= RightHandSide; index++)
        {
            if (index != column)
            {
                tableau[row, index] /= pivot;
            }
        }

        tableau[row, column] = 1m / pivot;
        (basic[row], nonbasic[column]) = (nonbasic[column], basic[row]);
    }
}
