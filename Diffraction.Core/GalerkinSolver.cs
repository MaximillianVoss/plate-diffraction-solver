using System;
using System.Threading;
using System.Threading.Tasks;
using static Diffraction.Core.DiffractionMath;

namespace Diffraction.Core
{
    public static class GalerkinSolver
    {
        public static DifrOnLenta SolveSinglePlate(
            double alpha,
            double beta,
            double wavelength,
            double incidenceAngleRadians,
            int approximationOrder,
            double skinDepth,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            DifrOnLenta solver = new DifrOnLenta(
                alpha,
                beta,
                wavelength,
                incidenceAngleRadians,
                approximationOrder,
                skinDepth);

            Compl sheetCoefficient = solver.SheetCoefficient;
            double waveNumber = 2.0 * Math.PI / wavelength;
            double halfLength = (beta - alpha) / 2.0;
            double midpoint = (alpha + beta) / 2.0;
            int quadratureOrder = Math.Max(8 * approximationOrder, 80);

            double[] tau = new double[quadratureOrder];
            double[] x = new double[quadratureOrder];
            double[] weights = new double[quadratureOrder];
            for (int m = 0; m < quadratureOrder; m++)
            {
                tau[m] = Math.Cos((2.0 * m + 1.0) / (2.0 * quadratureOrder) * Math.PI);
                x[m] = halfLength * tau[m] + midpoint;
                weights[m] = Math.PI / quadratureOrder * halfLength;
            }

            CMatr matrix = new CMatr(approximationOrder);
            CVect rightHandSide = new CVect(approximationOrder);
            double projectionWeight = Math.PI / quadratureOrder;
            double logarithmicConstant = Math.Log(waveNumber * halfLength / 2.0);

            double[][] basisValues = new double[approximationOrder][];
            for (int basisIndex = 0; basisIndex < approximationOrder; basisIndex++)
            {
                basisValues[basisIndex] = new double[quadratureOrder];
                for (int m = 0; m < quadratureOrder; m++)
                    basisValues[basisIndex][m] = Cheb(basisIndex, tau[m]);
            }

            Compl[][] regularKernel = new Compl[quadratureOrder][];
            Parallel.For(
                0,
                quadratureOrder,
                new ParallelOptions { CancellationToken = cancellationToken },
                targetIndex =>
                {
                    regularKernel[targetIndex] = new Compl[quadratureOrder];
                    for (int sourceIndex = 0; sourceIndex < quadratureOrder; sourceIndex++)
                    {
                        double argument = waveNumber * halfLength *
                            Math.Abs(tau[targetIndex] - tau[sourceIndex]);
                        regularKernel[targetIndex][sourceIndex] = R_H0(argument);
                    }
                });

            Compl[][] operatorValues = new Compl[quadratureOrder][];
            Parallel.For(
                0,
                quadratureOrder,
                new ParallelOptions { CancellationToken = cancellationToken },
                targetIndex =>
                {
                    operatorValues[targetIndex] = new Compl[approximationOrder];
                    double targetTau = tau[targetIndex];
                    double singularWeight = Math.Sqrt(Math.Max(1.0 - targetTau * targetTau, 1e-10));

                    for (int basisIndex = 0; basisIndex < approximationOrder; basisIndex++)
                    {
                        Compl regularPart = new Compl(0, 0);
                        for (int sourceIndex = 0; sourceIndex < quadratureOrder; sourceIndex++)
                        {
                            regularPart += regularKernel[targetIndex][sourceIndex] *
                                basisValues[basisIndex][sourceIndex] * weights[sourceIndex];
                        }

                        double orthogonalIntegral = basisIndex == 0 ? Math.PI : 0.0;
                        double logarithmicIntegral = basisIndex == 0
                            ? -Math.PI * Math.Log(2.0)
                            : -(Math.PI / basisIndex) * basisValues[basisIndex][targetIndex];
                        Compl singularPart = ci * (-2.0 / Math.PI) * halfLength *
                            (logarithmicConstant * orthogonalIntegral + logarithmicIntegral);
                        Compl value = ci / 4.0 * (regularPart + singularPart);
                        value -= sheetCoefficient / halfLength *
                            basisValues[basisIndex][targetIndex] / singularWeight;
                        operatorValues[targetIndex][basisIndex] = value;
                    }
                });

            Compl[] incidentValues = new Compl[quadratureOrder];
            for (int m = 0; m < quadratureOrder; m++)
                incidentValues[m] = solver.u0(x[m], 0);

            Parallel.For(
                0,
                approximationOrder,
                new ParallelOptions { CancellationToken = cancellationToken },
                testIndex =>
                {
                    for (int basisIndex = 0; basisIndex < approximationOrder; basisIndex++)
                    {
                        Compl projected = new Compl(0, 0);
                        for (int m = 0; m < quadratureOrder; m++)
                            projected += operatorValues[m][basisIndex] * basisValues[testIndex][m];
                        matrix[testIndex][basisIndex] = projected * projectionWeight;
                    }

                    Compl projectedRightHandSide = new Compl(0, 0);
                    for (int m = 0; m < quadratureOrder; m++)
                        projectedRightHandSide += -incidentValues[m] * basisValues[testIndex][m];
                    rightHandSide[testIndex] = projectedRightHandSide * projectionWeight;
                });

            CVect coefficients = new CVect(approximationOrder);
            int status = Gauss(matrix, rightHandSide, coefficients, cancellationToken);
            if (status != 1)
                throw new InvalidOperationException("Ошибка решения проекционной системы Галеркина.");

            Compl[] solvedCoefficients = new Compl[approximationOrder];
            for (int i = 0; i < approximationOrder; i++)
                solvedCoefficients[i] = new Compl(coefficients[i].Re, coefficients[i].Im);

            solver.ApplySolvedCoefficients(
                solvedCoefficients,
                "Галеркин (CPU, C#)",
                0,
                0,
                0,
                usedCuda: false);
            return solver;
        }
    }
}
