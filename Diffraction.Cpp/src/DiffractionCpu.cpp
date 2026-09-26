#include <algorithm>
#include <chrono>
#include <cmath>
#include <iomanip>
#include <iostream>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace
{
    constexpr double PI = 3.1415926535897932384626433832795;
    constexpr double GAMMA_E = 0.5772156649015328606065120900824;
    constexpr double MU0 = 4.0 * PI * 1e-7;
    constexpr double SPEED_OF_LIGHT = 299792458.0;
    constexpr double VACUUM_IMPEDANCE = MU0 * SPEED_OF_LIGHT;

    struct ComplexValue
    {
        double re;
        double im;

        ComplexValue() : re(0.0), im(0.0) {}
        ComplexValue(double real, double imag = 0.0) : re(real), im(imag) {}
    };

    ComplexValue operator+(const ComplexValue& a, const ComplexValue& b)
    {
        return ComplexValue(a.re + b.re, a.im + b.im);
    }

    ComplexValue operator-(const ComplexValue& a, const ComplexValue& b)
    {
        return ComplexValue(a.re - b.re, a.im - b.im);
    }

    ComplexValue operator-(const ComplexValue& a)
    {
        return ComplexValue(-a.re, -a.im);
    }

    ComplexValue operator*(const ComplexValue& a, const ComplexValue& b)
    {
        return ComplexValue(
            a.re * b.re - a.im * b.im,
            a.re * b.im + a.im * b.re);
    }

    ComplexValue operator*(const ComplexValue& a, double b)
    {
        return ComplexValue(a.re * b, a.im * b);
    }

    ComplexValue operator*(double a, const ComplexValue& b)
    {
        return ComplexValue(a * b.re, a * b.im);
    }

    ComplexValue operator/(const ComplexValue& a, double b)
    {
        return ComplexValue(a.re / b, a.im / b);
    }

    ComplexValue operator/(const ComplexValue& a, const ComplexValue& b)
    {
        double denom = b.re * b.re + b.im * b.im;
        return ComplexValue(
            (a.re * b.re + a.im * b.im) / denom,
            (b.re * a.im - b.im * a.re) / denom);
    }

    double abs_complex(const ComplexValue& value)
    {
        return std::sqrt(value.re * value.re + value.im * value.im);
    }

    double cheb(int n, double x)
    {
        if (n == 0) return 1.0;
        if (n == 1) return x;

        double t0 = 1.0;
        double t1 = x;
        double t = 0.0;
        for (int i = 2; i <= n; ++i)
        {
            t = 2.0 * x * t1 - t0;
            t0 = t1;
            t1 = t;
        }
        return t;
    }

    double legendre(int n, double x)
    {
        double previous = 1.0;
        if (n == 0) return previous;
        double current = x;
        for (int j = 1; j < n; ++j)
        {
            double next = ((2.0 * j + 1.0) * x * current - j * previous) / (j + 1.0);
            previous = current;
            current = next;
        }
        return current;
    }

    double legendre_log_moment(int n, double x)
    {
        if (x < -1.0 || x > 1.0)
            throw std::runtime_error("Logarithmic moment requires a point on the reference interval");
        if (x == -1.0 || x == 1.0)
        {
            if (n == 0) return 2.0 * std::log(2.0) - 2.0;
            double edge = -2.0 / (static_cast<double>(n) * (n + 1.0));
            return x < 0.0 && n % 2 != 0 ? -edge : edge;
        }
        if (n == 0)
            return (1.0 + x) * std::log1p(x) + (1.0 - x) * std::log1p(-x) - 2.0;

        // Integral of P_n(s) log|x-s| over [-1,1], without a Chebyshev weight.
        double previous = 0.5 * (std::log1p(x) - std::log1p(-x));
        double current = x * previous - 1.0;
        for (int j = 1; j <= n; ++j)
        {
            double next = ((2.0 * j + 1.0) * x * current - j * previous) / (j + 1.0);
            if (j == n) return 2.0 * (next - previous) / (2.0 * n + 1.0);
            previous = current;
            current = next;
        }
        throw std::runtime_error("Invalid Legendre degree");
    }

    void gauss_legendre(int n, std::vector<double>& nodes, std::vector<double>& weights)
    {
        nodes.resize(n);
        weights.resize(n);
        for (int i = 0; i < n; ++i)
        {
            double z = std::cos(PI * (i + 0.75) / (n + 0.5));
            bool converged = false;
            for (int iteration = 0; iteration < 100; ++iteration)
            {
                double p1 = 1.0;
                double p2 = 0.0;
                for (int j = 0; j < n; ++j)
                {
                    double p3 = p2;
                    p2 = p1;
                    p1 = ((2.0 * j + 1.0) * z * p2 - j * p3) / (j + 1.0);
                }
                double derivative = n * (z * p1 - p2) / (z * z - 1.0);
                double previous = z;
                z -= p1 / derivative;
                if (std::fabs(z - previous) <= 1e-14)
                {
                    double weight = 2.0 / ((1.0 - z * z) * derivative * derivative);
                    if (!std::isfinite(z) || !std::isfinite(weight) || std::fabs(z) >= 1.0 || weight <= 0.0)
                        throw std::runtime_error("Invalid Gauss-Legendre node or weight");
                    nodes[i] = z;
                    weights[i] = weight;
                    converged = true;
                    break;
                }
            }
            if (!converged)
                throw std::runtime_error("Gauss-Legendre quadrature did not converge");
        }
    }

    double j0_func(double x)
    {
        return std::cyl_bessel_j(0.0, std::fabs(x));
    }

    double n0_func(double x)
    {
        return std::cyl_neumann(0.0, x);
    }

    double j1_func(double x)
    {
        double value = std::cyl_bessel_j(1.0, std::fabs(x));
        return x < 0.0 ? -value : value;
    }

    double n1_func(double x)
    {
        return std::cyl_neumann(1.0, x);
    }

    double y0_regular(double x)
    {
        double j0 = j0_func(x);
        if (x > 1.0)
            return PI / 2.0 * n0_func(x) - j0 * std::log(x / 2.0);

        // Keep the logarithm analytically separated near the origin.
        double x_half_sq = x * x / 4.0;
        double sum = 0.0;
        double h_k = 0.0;
        double factorial_k_sq = 1.0;
        double x_pow = 1.0;

        for (int k = 1; k <= 100; ++k)
        {
            factorial_k_sq *= static_cast<double>(k) * static_cast<double>(k);
            x_pow *= x_half_sq;
            h_k += 1.0 / static_cast<double>(k);
            double sign = (k % 2 == 1) ? 1.0 : -1.0;
            double term = sign * x_pow / factorial_k_sq * h_k;
            sum += term;
            if (std::fabs(term) < 1e-15) break;
        }

        return GAMMA_E * j0 + sum;
    }

    ComplexValue h0_2(double x)
    {
        return ComplexValue(j0_func(x), -n0_func(x));
    }

    ComplexValue r_h0(double z)
    {
        if (z < 1e-12) return ComplexValue(1.0, -2.0 * GAMMA_E / PI);

        double j0 = j0_func(z);
        double lnz2 = std::log(z / 2.0);
        if (z > 1.0)
            return ComplexValue(j0, -n0_func(z) + (2.0 / PI) * lnz2);

        double y0reg = y0_regular(z);
        double re = j0;
        double im = (2.0 / PI) * lnz2 * (1.0 - j0) - (2.0 / PI) * y0reg;
        return ComplexValue(re, im);
    }

    ComplexValue exp_i(double phase)
    {
        return ComplexValue(std::cos(phase), std::sin(phase));
    }

    ComplexValue incident_field(double x, double z, double k, double theta)
    {
        return exp_i(k * std::cos(theta) * x + k * std::sin(theta) * z);
    }

    ComplexValue surface_impedance(double skin_depth, double lambda)
    {
        if (skin_depth <= 0.0) return ComplexValue();
        double frequency = SPEED_OF_LIGHT / lambda;
        double surface_resistance = PI * MU0 * frequency * skin_depth;
        return ComplexValue(surface_resistance, surface_resistance);
    }

    ComplexValue sheet_coefficient(const ComplexValue& chi, double k_wave)
    {
        if (chi.re == 0.0 && chi.im == 0.0) return ComplexValue();
        return ComplexValue(0.0, -1.0) * chi / (k_wave * VACUUM_IMPEDANCE);
    }

    struct SolverParameters
    {
        double alpha[2];
        double beta[2];
        double lambda;
        double theta;
        double skin_depth;
        int n;
        int m_quad;
        int plate_count;
        bool theta_set_in_degrees;
    };

    double half_length(const SolverParameters& params, int plate_index)
    {
        return (params.beta[plate_index] - params.alpha[plate_index]) / 2.0;
    }

    double midpoint(const SolverParameters& params, int plate_index)
    {
        return (params.beta[plate_index] + params.alpha[plate_index]) / 2.0;
    }

    double tau_to_x(const SolverParameters& params, int plate_index, double tau)
    {
        return half_length(params, plate_index) * tau + midpoint(params, plate_index);
    }

    SolverParameters parse_arguments(int argc, char** argv)
    {
        SolverParameters params{};
        params.alpha[0] = -1.5;
        params.beta[0] = -0.5;
        params.alpha[1] = 0.5;
        params.beta[1] = 1.5;
        params.lambda = 1.0;
        params.theta = 0.0;
        params.skin_depth = 0.0;
        params.n = 10;
        params.m_quad = 0;
        params.plate_count = 2;
        params.theta_set_in_degrees = false;

        for (int i = 1; i < argc; ++i)
        {
            if (i + 1 >= argc)
                throw std::runtime_error("Некорректные аргументы командной строки");

            const std::string key = argv[i];
            const std::string value = argv[++i];

            if (key == "--alpha1") params.alpha[0] = std::stod(value);
            else if (key == "--beta1") params.beta[0] = std::stod(value);
            else if (key == "--alpha2") params.alpha[1] = std::stod(value);
            else if (key == "--beta2") params.beta[1] = std::stod(value);
            else if (key == "--lambda") params.lambda = std::stod(value);
            else if (key == "--theta") params.theta = std::stod(value);
            else if (key == "--theta-deg")
            {
                params.theta = std::stod(value) * PI / 180.0;
                params.theta_set_in_degrees = true;
            }
            else if (key == "--skin-depth") params.skin_depth = std::stod(value);
            else if (key == "--n") params.n = std::stoi(value);
            else if (key == "--m-quad") params.m_quad = std::stoi(value);
            else throw std::runtime_error("Неизвестный аргумент: " + key);
        }

        return params;
    }

    void validate_parameters(const SolverParameters& params)
    {
        if (!std::isfinite(params.lambda) || !std::isfinite(params.theta) || !std::isfinite(params.skin_depth))
            throw std::runtime_error("Solver parameters must be finite");
        for (int p = 0; p < params.plate_count; ++p)
        {
            if (!std::isfinite(params.alpha[p]) || !std::isfinite(params.beta[p])
                || !std::isfinite(half_length(params, p)) || !std::isfinite(midpoint(params, p)))
                throw std::runtime_error("Plate coordinates and lengths must be finite");
        }
        if (params.n <= 0) throw std::runtime_error("N должен быть положительным");
        if (params.m_quad != 0 && params.m_quad <= 0) throw std::runtime_error("M должен быть положительным");
        long long total_unknowns = static_cast<long long>(params.n) * params.plate_count;
        if (total_unknowns > std::numeric_limits<int>::max() / total_unknowns
            || params.m_quad > std::numeric_limits<int>::max() / params.plate_count)
            throw std::runtime_error("N or M exceeds the supported index range");
        if (params.lambda <= 0.0) throw std::runtime_error("Длина волны должна быть положительной");
        if (params.skin_depth < 0.0) throw std::runtime_error("Толщина скин-слоя не может быть отрицательной");
        if (params.alpha[0] >= params.beta[0] || params.alpha[1] >= params.beta[1])
            throw std::runtime_error("Для каждой пластины должно выполняться alpha < beta");
        if (std::max(params.alpha[0], params.alpha[1]) < std::min(params.beta[0], params.beta[1]))
            throw std::runtime_error("Пластины не должны накладываться друг на друга");
        if (!params.theta_set_in_degrees && std::fabs(params.theta) > 2.0 * PI + 1e-12)
            throw std::runtime_error("Параметр --theta ожидается в радианах. Если угол задан в градусах, используйте --theta-deg.");
    }

    void build_quadrature(
        const SolverParameters& params,
        int m_quad,
        std::vector<double>& tau_q,
        std::vector<double>& w_q)
    {
        std::vector<double> nodes(m_quad);
        std::vector<double> weights(m_quad);
        if (params.skin_depth > 0.0)
        {
            gauss_legendre(m_quad, nodes, weights);
        }
        else
        {
            for (int m = 0; m < m_quad; ++m)
            {
                nodes[m] = std::cos((2.0 * m + 1.0) / (2.0 * m_quad) * PI);
                weights[m] = PI / m_quad;
            }
        }

        tau_q.resize(params.plate_count * m_quad);
        w_q.resize(params.plate_count * m_quad);
        for (int p = 0; p < params.plate_count; ++p)
        {
            double h = half_length(params, p);
            for (int m = 0; m < m_quad; ++m)
            {
                tau_q[p * m_quad + m] = nodes[m];
                w_q[p * m_quad + m] = weights[m] * h;
            }
        }
    }

    std::vector<double> build_tau_c(int plate_count, int n)
    {
        std::vector<double> values(plate_count * n);
        for (int p = 0; p < plate_count; ++p)
        {
            for (int i = 0; i < n; ++i)
                values[p * n + i] = std::cos((i + 0.5) / n * PI);
        }
        return values;
    }

    std::vector<double> build_t_q(const SolverParameters& params, const std::vector<double>& tau_q, int m_quad)
    {
        std::vector<double> values(params.plate_count * m_quad);
        for (int p = 0; p < params.plate_count; ++p)
        {
            for (int m = 0; m < m_quad; ++m)
                values[p * m_quad + m] = tau_to_x(params, p, tau_q[p * m_quad + m]);
        }
        return values;
    }

    std::vector<double> build_x_c(const SolverParameters& params, const std::vector<double>& tau_c)
    {
        std::vector<double> values(params.plate_count * params.n);
        for (int p = 0; p < params.plate_count; ++p)
        {
            for (int i = 0; i < params.n; ++i)
                values[p * params.n + i] = tau_to_x(params, p, tau_c[p * params.n + i]);
        }
        return values;
    }

    void assemble_matrix_cpu(
        std::vector<ComplexValue>& matrix,
        const SolverParameters& params,
        const std::vector<double>& tau_q,
        const std::vector<double>& t_q,
        const std::vector<double>& w_q,
        const std::vector<double>& tau_c,
        const std::vector<double>& x_c,
        int m_quad,
        double k_wave,
        ComplexValue sheet_q)
    {
        int total_unknowns = params.n * params.plate_count;
        matrix.assign(total_unknowns * total_unknowns, ComplexValue());
        bool regular_current = params.skin_depth > 0.0;

        for (int row = 0; row < total_unknowns; ++row)
        {
            int target_plate = row / params.n;
            int ik = row % params.n;
            double target_half_length = half_length(params, target_plate);
            double xk = x_c[target_plate * params.n + ik];
            double tau_k = tau_c[target_plate * params.n + ik];

            for (int source_plate = 0; source_plate < params.plate_count; ++source_plate)
            {
                for (int j = 0; j < params.n; ++j)
                {
                    int col = source_plate * params.n + j;
                    ComplexValue result;
                    ComplexValue ci(0.0, 1.0);

                    if (source_plate == target_plate)
                    {
                        ComplexValue sum_reg;
                        for (int m = 0; m < m_quad; ++m)
                        {
                            int offset = target_plate * m_quad + m;
                            double kd = k_wave * target_half_length * std::fabs(tau_k - tau_q[offset]);
                            ComplexValue regular = r_h0(kd);
                            double tj = regular_current ? legendre(j, tau_q[offset]) : cheb(j, tau_q[offset]);
                            sum_reg = sum_reg + regular * (tj * w_q[offset]);
                        }

                        double ln_const = std::log(k_wave * target_half_length / 2.0);
                        double i_ortho = (j == 0) ? (regular_current ? 2.0 : PI) : 0.0;
                        double i_log = regular_current
                            ? legendre_log_moment(j, tau_k)
                            : ((j == 0) ? (-PI * std::log(2.0)) : (-(PI / static_cast<double>(j)) * cheb(j, tau_k)));
                        ComplexValue s_log = ci * ((-2.0 / PI) * target_half_length * (ln_const * i_ortho + i_log));
                        result = (sum_reg + s_log) * ComplexValue(0.0, 0.25);

                        if (regular_current)
                        {
                            result = result - sheet_q * legendre(j, tau_k);
                        }
                    }
                    else
                    {
                        ComplexValue sum_cross;
                        for (int m = 0; m < m_quad; ++m)
                        {
                            int offset = source_plate * m_quad + m;
                            double distance = std::fabs(t_q[offset] - xk);
                            if (distance < 1e-14) distance = 1e-14;
                            double tj = regular_current ? legendre(j, tau_q[offset]) : cheb(j, tau_q[offset]);
                            sum_cross = sum_cross + h0_2(k_wave * distance) * (tj * w_q[offset]);
                        }

                        result = sum_cross * ComplexValue(0.0, 0.25);
                    }

                    matrix[row * total_unknowns + col] = result;
                }
            }
        }
    }

    std::vector<ComplexValue> assemble_rhs_cpu(
        const SolverParameters& params,
        const std::vector<double>& x_c,
        double k_wave)
    {
        int total_unknowns = params.n * params.plate_count;
        std::vector<ComplexValue> rhs(total_unknowns);

        for (int row = 0; row < total_unknowns; ++row)
        {
            int target_plate = row / params.n;
            int ik = row % params.n;
            double xk = x_c[target_plate * params.n + ik];
            rhs[row] = -incident_field(xk, 0.0, k_wave, params.theta);
        }

        return rhs;
    }

    void gaussian_elimination(
        std::vector<ComplexValue>& matrix,
        std::vector<ComplexValue>& rhs,
        std::vector<ComplexValue>& solution,
        int total_unknowns)
    {
        for (int i = 0; i < total_unknowns - 1; ++i)
        {
            double max_value = abs_complex(matrix[i * total_unknowns + i]);
            int pivot_row = i;
            for (int k = i + 1; k < total_unknowns; ++k)
            {
                double current = abs_complex(matrix[k * total_unknowns + i]);
                if (current > max_value)
                {
                    max_value = current;
                    pivot_row = k;
                }
            }

            if (pivot_row != i)
            {
                for (int k = 0; k < total_unknowns; ++k)
                    std::swap(matrix[i * total_unknowns + k], matrix[pivot_row * total_unknowns + k]);
                std::swap(rhs[i], rhs[pivot_row]);
            }

            ComplexValue pivot = matrix[i * total_unknowns + i];
            if (abs_complex(pivot) < 1e-12)
                throw std::runtime_error("Система вырождена: нулевой ведущий элемент");

            for (int j = i + 1; j < total_unknowns; ++j)
            {
                ComplexValue factor = matrix[j * total_unknowns + i] / pivot;
                for (int k = i + 1; k < total_unknowns; ++k)
                {
                    matrix[j * total_unknowns + k] =
                        matrix[j * total_unknowns + k] - factor * matrix[i * total_unknowns + k];
                }
                matrix[j * total_unknowns + i] = ComplexValue();
                rhs[j] = rhs[j] - factor * rhs[i];
            }
        }

        if (abs_complex(matrix[(total_unknowns - 1) * total_unknowns + total_unknowns - 1]) < 1e-12)
            throw std::runtime_error("Система вырождена на обратном ходе");

        solution.assign(total_unknowns, ComplexValue());
        solution[total_unknowns - 1] =
            rhs[total_unknowns - 1] / matrix[(total_unknowns - 1) * total_unknowns + total_unknowns - 1];

        for (int i = total_unknowns - 2; i >= 0; --i)
        {
            ComplexValue sum = rhs[i];
            for (int j = total_unknowns - 1; j > i; --j)
                sum = sum - matrix[i * total_unknowns + j] * solution[j];
            solution[i] = sum / matrix[i * total_unknowns + i];
        }
    }
}

int main(int argc, char** argv)
{
    try
    {
        SolverParameters params = parse_arguments(argc, argv);
        validate_parameters(params);

        int m_quad = params.m_quad > 0 ? params.m_quad : std::max(8 * params.n, 80);
        int total_unknowns = params.n * params.plate_count;
        double k_wave = 2.0 * PI / params.lambda;
        ComplexValue chi = surface_impedance(params.skin_depth, params.lambda);
        ComplexValue sheet_q = sheet_coefficient(chi, k_wave);
        double max_argument = k_wave * (std::max(params.beta[0], params.beta[1]) - std::min(params.alpha[0], params.alpha[1]));
        if (!std::isfinite(k_wave) || k_wave <= 0.0 || !std::isfinite(max_argument)
            || !std::isfinite(chi.re) || !std::isfinite(chi.im)
            || !std::isfinite(sheet_q.re) || !std::isfinite(sheet_q.im)
            || !std::isfinite(params.theta * 180.0 / PI))
            throw std::runtime_error("Derived solver parameters must be finite");

        std::vector<double> tau_q;
        std::vector<double> w_q;
        build_quadrature(params, m_quad, tau_q, w_q);
        std::vector<double> tau_c = build_tau_c(params.plate_count, params.n);
        std::vector<double> t_q = build_t_q(params, tau_q, m_quad);
        std::vector<double> x_c = build_x_c(params, tau_c);

        auto total_start = std::chrono::high_resolution_clock::now();

        auto assembly_start = std::chrono::high_resolution_clock::now();
        std::vector<ComplexValue> matrix;
        assemble_matrix_cpu(matrix, params, tau_q, t_q, w_q, tau_c, x_c, m_quad, k_wave, sheet_q);
        std::vector<ComplexValue> rhs = assemble_rhs_cpu(params, x_c, k_wave);
        auto assembly_stop = std::chrono::high_resolution_clock::now();

        auto solve_start = std::chrono::high_resolution_clock::now();
        std::vector<ComplexValue> solution;
        gaussian_elimination(matrix, rhs, solution, total_unknowns);
        auto solve_stop = std::chrono::high_resolution_clock::now();

        auto total_stop = std::chrono::high_resolution_clock::now();

        double assembly_ms = std::chrono::duration<double, std::milli>(assembly_stop - assembly_start).count();
        double solve_ms = std::chrono::duration<double, std::milli>(solve_stop - solve_start).count();
        double total_ms = std::chrono::duration<double, std::milli>(total_stop - total_start).count();

        for (int i = 0; i < total_unknowns; ++i)
        {
            if (!std::isfinite(solution[i].re) || !std::isfinite(solution[i].im))
                throw std::runtime_error("Non-finite solution coefficient at index " + std::to_string(i));
        }

        std::cout << std::setprecision(17);
        std::cout << "status=ok\n";
        std::cout << "backend=CPU C++ (matrix + solve)\n";
        std::cout << "model=thin_sheet_v4\n";
        std::cout << "alpha1=" << params.alpha[0] << "\n";
        std::cout << "beta1=" << params.beta[0] << "\n";
        std::cout << "alpha2=" << params.alpha[1] << "\n";
        std::cout << "beta2=" << params.beta[1] << "\n";
        std::cout << "lambda=" << params.lambda << "\n";
        std::cout << "theta_rad=" << params.theta << "\n";
        std::cout << "theta_deg=" << (params.theta * 180.0 / PI) << "\n";
        std::cout << "skin_depth=" << params.skin_depth << "\n";
        std::cout << "n=" << params.n << "\n";
        std::cout << "m_quad=" << m_quad << "\n";
        std::cout << "chi_re=" << chi.re << "\n";
        std::cout << "chi_im=" << chi.im << "\n";
        std::cout << "assembly_ms=" << assembly_ms << "\n";
        std::cout << "solve_ms=" << solve_ms << "\n";
        std::cout << "total_ms=" << total_ms << "\n";
        for (int i = 0; i < static_cast<int>(solution.size()); ++i)
            std::cout << "coeff_" << i << "=" << solution[i].re << "," << solution[i].im << "\n";
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "status=error\n";
        std::cerr << "message=" << ex.what() << "\n";
        return 1;
    }
}
