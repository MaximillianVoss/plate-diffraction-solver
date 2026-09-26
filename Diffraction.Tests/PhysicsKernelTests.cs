using System;
using Diffraction.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Diffraction.Core.DiffractionMath;

namespace Diffraction.Tests
{
    [TestClass]
    public class PhysicsKernelTests
    {
        // Independent reference: mpmath 1.4.1, 80 decimal digits; not solver snapshots.
        [DataTestMethod]
        [DataRow(0.000001, 0.99999999999975, -8.869031481659444, 0.0000004999999999999375, -636619.772372175)]
        [DataRow(1.0, 0.7651976865579666, 0.08825696421567696, 0.4400505857449335, -0.7812128213002887)]
        [DataRow(8.0, 0.1716508071375539, 0.2235214893875662, 0.2346363468539146, -0.1580604617312475)]
        [DataRow(20.0, 0.1670246643405832, 0.06264059680938383, 0.06683312417585005, -0.1655116143625213)]
        [DataRow(40.0, 0.00736689058423729, 0.1259364170582609, 0.1260383180375850, -0.005793505821549633)]
        [DataRow(80.0, -0.06974216551221002, -0.05562033908977000, -0.05605729667571258, 0.06939591378458805)]
        [DataRow(200.0, -0.01543743993056509, -0.05426577524981791, -0.05430453818237822, 0.01530182458038999)]
        public void BesselFunctionsAgreeWithHighPrecisionReference(double x, double j0, double y0, double j1, double y1)
        {
            Near(j0, J0(x), "J0");
            Near(y0, N0(x), "Y0");
            Near(j1, J1(x), "J1");
            Near(y1, N1(x), "Y1");
        }

        [DataTestMethod]
        [DataRow(0.1)]
        [DataRow(1.0)]
        [DataRow(8.0)]
        [DataRow(40.0)]
        public void HankelDerivativeAgreesWithKernel(double x)
        {
            double h = 0.00001 * Math.Max(1, x);
            Compl derivative = (H0_2(x + h) - H0_2(x - h)) / (2 * h);
            Assert.IsTrue(Compl.Abs(derivative + H1_2(x)) < 2e-7,
                "dH0^(2)/dx must equal -H1^(2)");
        }

        [DataTestMethod]
        [DataRow(0.5)]
        [DataRow(1.0)]
        [DataRow(2.0)]
        [DataRow(4.0)]
        public void GalerkinUsesTheSameSheetConditionAtEveryPlateLength(double length)
        {
            DifrOnLenta solver = GalerkinSolver.SolveSinglePlate(-length / 2, length / 2, 1, Math.PI / 4, 40, 0.01);
            double incident = solver.CalculatePlateIncidentEnergy();
            Assert.IsTrue(solver.VerifyBoundaryConditions() < 0.02, "u-qJ residual");
            Assert.IsTrue(Math.Abs(solver.CalculateAbsorbedEnergy() - solver.CalculateAbsorbedEnergyByBoundaryValue()) / incident < 0.01,
                "Independent absorption forms disagree");
        }

        [DataTestMethod]
        [DataRow(10.0, 0.0, false)]
        [DataRow(10.0, 0.01, false)]
        [DataRow(45.0, 0.01, false)]
        [DataRow(90.0, 0.01, false)]
        [DataRow(10.0, 0.01, true)]
        [DataRow(45.0, 0.01, true)]
        public void OpticalTheoremBalancesIndependentSourceWorkAndFarField(double degrees, double skin, bool galerkin)
        {
            DifrOnLenta solver = galerkin
                ? GalerkinSolver.SolveSinglePlate(-0.5, 0.5, 1, degrees * Math.PI / 180, 40, skin)
                : new DifrOnLenta(-0.5, 0.5, 1, degrees * Math.PI / 180, 40, skin);
            if (!galerkin) Assert.AreEqual(1, solver.SolveDifr());
            double extinction = IncidentWork(solver, 640);
            Assert.AreEqual(extinction, solver.CalculateExtinctionEnergy(640), 1e-10,
                "Forward amplitude must match independent incident-current work");
            double scattered = solver.CalculateFarFieldScatteredEnergy(360, 320).TotalScattered;
            double absorbed = solver.CalculateAbsorbedEnergy();
            Assert.IsTrue(extinction > 0, "passive extinction");
            Assert.IsTrue(Math.Abs(extinction - scattered - absorbed) / extinction < 0.005,
                string.Format("Pext={0:R}; Pscat={1:R}; Pabs={2:R}", extinction, scattered, absorbed));
        }

        [DataTestMethod]
        [DataRow(false, 0.1)]
        [DataRow(false, 10.0)]
        [DataRow(true, 0.1)]
        [DataRow(true, 10.0)]
        public void UniformLengthScalingPreservesDimensionlessPhysics(bool galerkin, double scale)
        {
            DifrOnLenta reference = Solve(galerkin, -0.5, 0.5, 1, 30, 0.01);
            DifrOnLenta scaled = Solve(galerkin, -0.5 * scale, 0.5 * scale, scale, 30, 0.01 * scale);
            Assert.AreEqual(reference.CalculateExtinctionEnergy(320), scaled.CalculateExtinctionEnergy(320), 1e-9);
            Assert.AreEqual(reference.CalculateAbsorbedEnergy(), scaled.CalculateAbsorbedEnergy(), 1e-9);
            Assert.IsTrue(Compl.Abs(reference.u(0.1, 0.2) - scaled.u(0.1 * scale, 0.2 * scale)) < 1e-9);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TranslationPreservesPowerAndForwardPhase(bool galerkin)
        {
            DifrOnLenta reference = Solve(galerkin, -0.5, 0.5, 1, 30, 0.01);
            DifrOnLenta shifted = Solve(galerkin, 9.5, 10.5, 1, 30, 0.01);
            Assert.AreEqual(reference.CalculateExtinctionEnergy(320), shifted.CalculateExtinctionEnergy(320), 1e-9);
            Assert.AreEqual(reference.CalculateAbsorbedEnergy(), shifted.CalculateAbsorbedEnergy(), 1e-9);
            Assert.AreEqual(reference.CalculateFarFieldScatteredEnergy(180, 320).TotalScattered,
                shifted.CalculateFarFieldScatteredEnergy(180, 320).TotalScattered, 1e-9);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GrazingCaseConvergesWithOrderWithoutClippingPower(bool galerkin)
        {
            DifrOnLenta coarse = Solve(galerkin, -0.5, 0.5, 1, 30, 0.01);
            DifrOnLenta fine = Solve(galerkin, -0.5, 0.5, 1, 55, 0.01);
            double coarseExt = coarse.CalculateExtinctionEnergy(640);
            double fineExt = fine.CalculateExtinctionEnergy(640);
            Assert.IsTrue(Math.Abs(coarseExt - fineExt) / fineExt < 0.002);
            Assert.IsTrue(Math.Abs(coarse.CalculateAbsorbedEnergy() - fine.CalculateAbsorbedEnergy()) /
                fineExt < 0.002);
            Assert.IsTrue(fine.CalculateFarFieldScatteredEnergy(180, 320).ReflectedScattered /
                fine.CalculatePlateIncidentEnergy() > 2.0, "A finite-strip cross section must not be clipped to 100%.");
        }

        [TestMethod]
        public void LongPlateHasFiniteFieldAndClosesTheIndependentEnergyBalance()
        {
            DifrOnLenta solver = new DifrOnLenta(-4, 4, 1, Math.PI / 4, 60, 0.01);
            Assert.AreEqual(1, solver.SolveDifr());
            double extinction = solver.CalculateExtinctionEnergy(640);
            double scattered = solver.CalculateFarFieldScatteredEnergy(720, 640).TotalScattered;
            double absorption = solver.CalculateAbsorbedEnergy();
            Assert.IsTrue(Math.Abs(extinction - scattered - absorption) / extinction < 0.005);
            Assert.IsTrue(solver.VerifyHelmholtz() < 0.001);
            Compl distant = solver.u(100, 100);
            Assert.IsFalse(double.IsNaN(distant.Re) || double.IsInfinity(distant.Re));
            Assert.IsTrue(Compl.Abs(distant) < 2.0, "Outgoing cylindrical waves must decay, not explode.");
        }

        [TestMethod]
        public void ConductivityConvertsMicrometersToSI()
        {
            var solver = new DifrOnLenta(-0.5, 0.5, 1, Math.PI / 4, 10, 0.01);
            double mu0 = 4 * Math.PI * 1e-7;
            double frequency = 299792458.0 / 1e-6;
            double deltaMeters = 0.01e-6;
            double expected = 1 / (Math.PI * mu0 * frequency * deltaMeters * deltaMeters);
            Assert.AreEqual(expected, solver.CalculateConductivity(0.01, 1), expected * 1e-12);
        }

        [DataTestMethod]
        [DataRow(double.NaN, 1.0, 1.0, 0.1, 0.01)]
        [DataRow(-1.0, double.PositiveInfinity, 1.0, 0.1, 0.01)]
        [DataRow(-1.0, 1.0, double.NaN, 0.1, 0.01)]
        [DataRow(-1.0, 1.0, 1.0, double.NaN, 0.01)]
        [DataRow(-1.0, 1.0, 1.0, 0.1, double.NaN)]
        public void CoreRejectsNonfinitePhysicalInputs(double a, double b, double wavelength, double angle, double skin)
        {
            Assert.ThrowsException<ArgumentException>(() => new DifrOnLenta(a, b, wavelength, angle, 10, skin));
        }

        [TestMethod]
        public void FiniteSkinCurrentStaysBoundedAtPlateEnds()
        {
            var solver = new DifrOnLenta(-0.5, 0.5, 1, Math.PI / 4, 40, 0.01);
            Assert.AreEqual(1, solver.SolveDifr());
            foreach (double sign in new[] { -1.0, 1.0 })
            {
                double near = Compl.Abs(solver.CurrentDensity(sign * (0.5 - 1e-6)));
                double atEnd = Compl.Abs(solver.CurrentDensity(sign * 0.5));
                Assert.IsTrue(atEnd < 2 * Math.Max(near, 1.0),
                    "Finite impedance requires an L2 current, not an arbitrary edge cutoff.");
            }
        }

        // Independent direct integration, mpmath at 60 decimal digits, split at t=x.
        [DataTestMethod]
        [DataRow(0, 0.2, -1.959728972898622253)]
        [DataRow(1, 0.2, -0.394623251891918903)]
        [DataRow(2, -0.7, -0.132959621720110324)]
        [DataRow(5, 0.95, -0.047054375422851157)]
        [DataRow(30, 0.2, 0.014582151486636064)]
        [DataRow(55, 0.999999, -0.000655666061621052)]
        [DataRow(199, -0.3, 0.000772468404070244)]
        public void LegendreLogMomentsMatchIndependentHighPrecisionIntegrals(int order, double x, double expected)
        {
            Assert.AreEqual(expected, LegendreLogMoment(order, x), 5e-13);
            Assert.AreEqual(-2.0 / ((order + 1.0) * (order + 2.0)),
                LegendreLogMoment(order + 1, 1), 1e-15);
            double sign = (order + 1) % 2 == 0 ? 1 : -1;
            Assert.AreEqual(sign * LegendreLogMoment(order + 1, 1),
                LegendreLogMoment(order + 1, -1), 1e-15);
        }

        private static DifrOnLenta Solve(bool galerkin, double a, double b, double wavelength, int order, double skin)
        {
            if (galerkin) return GalerkinSolver.SolveSinglePlate(a, b, wavelength, Math.PI / 18, order, skin);
            var solver = new DifrOnLenta(a, b, wavelength, Math.PI / 18, order, skin);
            Assert.AreEqual(1, solver.SolveDifr());
            return solver;
        }

        private static double IncidentWork(DifrOnLenta solver, int samples)
        {
            // Integrate Im(conj(u_inc)*J)/2 directly, independently of the far-field API.
            double sum = 0;
            for (int plate = 0; plate < solver.PlateCount; plate++)
            {
                double half = (solver.beta[plate] - solver.alpha[plate]) / 2;
                double center = (solver.beta[plate] + solver.alpha[plate]) / 2;
                double[] regularNodes = null, regularWeights = null;
                if (!solver.UsesSingularCurrentBasis)
                    GaussLegendreQuadrature(samples, out regularNodes, out regularWeights);
                for (int i = 0; i < samples; i++)
                {
                    double t = Math.PI * (i + 0.5) / samples;
                    double x = center + half * (solver.UsesSingularCurrentBasis ? Math.Cos(t) : regularNodes[i]);
                    Compl current = solver.CurrentDensity(x);
                    Compl incident = solver.u0(x, 0);
                    sum += 0.5 * (incident.Re * current.Im - incident.Im * current.Re) *
                        half * (solver.UsesSingularCurrentBasis ? Math.Sin(t) * Math.PI / samples : regularWeights[i]);
                }
            }
            return sum;
        }

        private static void Near(double expected, double actual, string name)
        {
            Assert.AreEqual(expected, actual, 2e-13 * Math.Max(1, Math.Abs(expected)), name);
        }
    }
}
