#include <cuda_runtime.h>
#include <cusolverDn.h>
#include <cuComplex.h>

#include <chrono>
#include <cmath>
#include <cstring>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace
{
    constexpr double PI = 3.1415926535897932384626433832795;
    constexpr double GAMMA_E = 0.5772156649015328606065120900824;

    struct ComplexValue
    {
        double re;
        double im;

        __host__ __device__ ComplexValue() : re(0.0), im(0.0) {}
        __host__ __device__ ComplexValue(double real, double imag = 0.0) : re(real), im(imag) {}
    };

    __host__ __device__ ComplexValue operator+(const ComplexValue& a, const ComplexValue& b)
    {
        return ComplexValue(a.re + b.re, a.im + b.im);
    }

    __host__ __device__ ComplexValue operator-(const ComplexValue& a, const ComplexValue& b)
    {
        return ComplexValue(a.re - b.re, a.im - b.im);
    }

    __host__ __device__ ComplexValue operator-(const ComplexValue& a)
    {
        return ComplexValue(-a.re, -a.im);
    }

    __host__ __device__ ComplexValue operator*(const ComplexValue& a, const ComplexValue& b)
    {
        return ComplexValue(
            a.re * b.re - a.im * b.im,
            a.re * b.im + a.im * b.re);
    }

    __host__ __device__ ComplexValue operator*(const ComplexValue& a, double b)
    {
        return ComplexValue(a.re * b, a.im * b);
    }

    __host__ __device__ ComplexValue operator*(double a, const ComplexValue& b)
    {
        return ComplexValue(a * b.re, a * b.im);
    }

    __host__ __device__ ComplexValue operator/(const ComplexValue& a, double b)
    {
        return ComplexValue(a.re / b, a.im / b);
    }

    __host__ __device__ ComplexValue operator/(const ComplexValue& a, const ComplexValue& b)
    {
        double denom = b.re * b.re + b.im * b.im;
        return ComplexValue(
            (a.re * b.re + a.im * b.im) / denom,
            (b.re * a.im - b.im * a.re) / denom);
    }

    __host__ __device__ double abs_complex(const ComplexValue& value)
    {
        return sqrt(value.re * value.re + value.im * value.im);
    }

    __host__ __device__ double cheb(int n, double x)
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

    __host__ __device__ double j0_series(double x)
    {
        double x_half_sq = x * x / 4.0;
        double sum = 1.0;
        double term = 1.0;
        for (int k = 1; k <= 100; ++k)
        {
            term *= -x_half_sq / (static_cast<double>(k) * static_cast<double>(k));
            sum += term;
            if (fabs(term) < 1e-15) break;
        }
        return sum;
    }

    __host__ __device__ double y0_regular(double x)
    {
        double j0 = j0_series(x);
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
            if (fabs(term) < 1e-15) break;
        }

        return GAMMA_E * j0 + sum;
    }

    __host__ __device__ double n0_func(double x)
    {
        return 2.0 / PI * (j0_series(x) * log(x / 2.0) + y0_regular(x));
    }

    __host__ __device__ double j1_series(double x)
    {
        if (fabs(x) < 1e-10) return 0.0;

        double x_half = x / 2.0;
        double x_half_sq = x_half * x_half;
        double sum = x_half;
        double term = x_half;

        for (int k = 1; k <= 100; ++k)
        {
            term *= -x_half_sq / (static_cast<double>(k) * static_cast<double>(k + 1));
            sum += term;
            if (fabs(term) < 1e-15) break;
        }

        return sum;
    }

    __host__ __device__ double y1_regular(double x)
    {
        double x_half = x / 2.0;
        double x_half_sq = x_half * x_half;
        double h_k = 0.0;
        double x_pow = x_half;
        double fact_k = 1.0;
        double fact_k1 = 1.0;
        double sum = -1.0 / x;

        for (int k = 0; k <= 100; ++k)
        {
            if (k > 0)
            {
                fact_k *= static_cast<double>(k);
                fact_k1 *= static_cast<double>(k + 1);
                x_pow *= -x_half_sq;
                h_k += 1.0 / static_cast<double>(k);
            }

            double h_k1 = h_k + 1.0 / static_cast<double>(k + 1);
            double term = x_pow / (fact_k * fact_k1) * (h_k + h_k1);
            sum += term;
            if (k > 0 && fabs(term) < 1e-15) break;
        }

        return sum;
    }

    __host__ __device__ double n1_func(double x)
    {
        if (fabs(x) < 1e-10) return -1e300;
        return 2.0 / PI * (j1_series(x) * log(x / 2.0) + y1_regular(x));
    }

    __host__ __device__ ComplexValue h0_2(double x)
    {
        return ComplexValue(j0_series(x), -n0_func(x));
    }

    __host__ __device__ ComplexValue h1_2(double x)
    {
        return ComplexValue(j1_series(x), -n1_func(x));
    }

    __host__ __device__ ComplexValue r_h0(double z)
    {
        if (z < 1e-12) return ComplexValue(1.0, -2.0 * GAMMA_E / PI);

        double j0 = j0_series(z);
        double lnz2 = log(z / 2.0);
        double y0reg = y0_regular(z);
        double re = j0;
        double im = (2.0 / PI) * lnz2 * (1.0 - j0) - (2.0 / PI) * y0reg;
        return ComplexValue(re, im);
    }

    __host__ __device__ ComplexValue exp_i(double phase)
    {
        return ComplexValue(cos(phase), sin(phase));
    }

    __host__ __device__ ComplexValue incident_field(double x, double z, double k, double theta)
    {
        return exp_i(k * cos(theta) * x + k * sin(theta) * z);
    }

    __host__ __device__ double half_length(const double* alpha, const double* beta, int plate_index)
    {
        return (beta[plate_index] - alpha[plate_index]) / 2.0;
    }

    __host__ __device__ double midpoint(const double* alpha, const double* beta, int plate_index)
    {
        return (beta[plate_index] + alpha[plate_index]) / 2.0;
    }

    __host__ __device__ double tau_to_x(const double* alpha, const double* beta, int plate_index, double tau)
    {
        return half_length(alpha, beta, plate_index) * tau + midpoint(alpha, beta, plate_index);
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

    __global__ void assemble_matrix_kernel(
        cuDoubleComplex* matrix,
        const double* alpha,
        const double* beta,
        const double* tau_q,
        const double* t_q,
        const double* w_q,
        const double* tau_c,
        const double* x_c,
        int n,
        int plate_count,
        int m_quad,
        double k_wave,
        ComplexValue chi)
    {
        int total_unknowns = n * plate_count;
        int index = blockIdx.x * blockDim.x + threadIdx.x;
        if (index >= total_unknowns * total_unknowns) return;

        int row = index / total_unknowns;
        int col = index % total_unknowns;

        int target_plate = row / n;
        int ik = row % n;
        int source_plate = col / n;
        int j = col % n;

        double target_half_length = half_length(alpha, beta, target_plate);
        double xk = x_c[target_plate * n + ik];
        double tau_k = tau_c[target_plate * n + ik];

        ComplexValue result;
        ComplexValue ci(0.0, 1.0);

        if (source_plate == target_plate)
        {
            ComplexValue sum_reg;
            for (int m = 0; m < m_quad; ++m)
            {
                int offset = target_plate * m_quad + m;
                double kd = k_wave * target_half_length * fabs(tau_k - tau_q[offset]);
                ComplexValue regular = r_h0(kd);
                double tj = cheb(j, tau_q[offset]);
                sum_reg = sum_reg + regular * (tj * w_q[offset]);
            }

            double ln_const = log(k_wave * target_half_length / 2.0);
            double i_ortho = (j == 0) ? PI : 0.0;
            double i_log = (j == 0) ? (-PI * log(2.0)) : (-(PI / static_cast<double>(j)) * cheb(j, tau_k));
            ComplexValue s_log = ci * ((-2.0 / PI) * target_half_length * (ln_const * i_ortho + i_log));
            result = (sum_reg + s_log) * ComplexValue(0.0, 0.25);

            if (chi.re != 0.0 || chi.im != 0.0)
            {
                double tj_k = cheb(j, tau_k);
                double sqrt_w = sqrt(1.0 - tau_k * tau_k);
                result = result - chi / (2.0 * target_half_length) * (tj_k / sqrt_w);
            }
        }
        else
        {
            ComplexValue sum_cross;
            for (int m = 0; m < m_quad; ++m)
            {
                int offset = source_plate * m_quad + m;
                double distance = fabs(t_q[offset] - xk);
                if (distance < 1e-14) distance = 1e-14;
                double tj = cheb(j, tau_q[offset]);
                sum_cross = sum_cross + h0_2(k_wave * distance) * (tj * w_q[offset]);
            }

            result = sum_cross * ComplexValue(0.0, 0.25);
        }

        matrix[col * total_unknowns + row] = make_cuDoubleComplex(result.re, result.im);
    }

    __global__ void assemble_rhs_kernel(
        cuDoubleComplex* rhs,
        const double* x_c,
        int n,
        int plate_count,
        double theta,
        double k_wave,
        ComplexValue chi)
    {
        int total_unknowns = n * plate_count;
        int row = blockIdx.x * blockDim.x + threadIdx.x;
        if (row >= total_unknowns) return;

        int target_plate = row / n;
        int ik = row % n;
        double xk = x_c[target_plate * n + ik];
        ComplexValue u0 = incident_field(xk, 0.0, k_wave, theta);
        ComplexValue ci(0.0, 1.0);

        if (chi.re != 0.0 || chi.im != 0.0)
        {
            ComplexValue du0_dz = ci * (k_wave * sin(theta)) * u0;
            ComplexValue rhs_value = -u0 - chi * du0_dz;
            rhs[row] = make_cuDoubleComplex(rhs_value.re, rhs_value.im);
        }
        else
        {
            rhs[row] = make_cuDoubleComplex(-u0.re, -u0.im);
        }
    }

    void check_cuda(cudaError_t error, const char* message)
    {
        if (error != cudaSuccess)
        {
            std::ostringstream stream;
            stream << message << ": " << cudaGetErrorString(error);
            throw std::runtime_error(stream.str());
        }
    }

    void check_cusolver(cusolverStatus_t status, const char* message)
    {
        if (status != CUSOLVER_STATUS_SUCCESS)
        {
            std::ostringstream stream;
            stream << message << ": status=" << static_cast<int>(status);
            throw std::runtime_error(stream.str());
        }
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
        if (params.n <= 0) throw std::runtime_error("N должен быть положительным");
        if (params.m_quad != 0 && params.m_quad <= 0) throw std::runtime_error("M должен быть положительным");
        if (params.lambda <= 0.0) throw std::runtime_error("Длина волны должна быть положительной");
        if (params.skin_depth < 0.0) throw std::runtime_error("Толщина скин-слоя не может быть отрицательной");
        if (params.alpha[0] >= params.beta[0] || params.alpha[1] >= params.beta[1])
            throw std::runtime_error("Для каждой пластины должно выполняться alpha < beta");
        if (fmax(params.alpha[0], params.alpha[1]) < fmin(params.beta[0], params.beta[1]))
            throw std::runtime_error("Пластины не должны накладываться друг на друга");
        if (!params.theta_set_in_degrees && std::fabs(params.theta) > 2.0 * PI + 1e-12)
            throw std::runtime_error("Параметр --theta ожидается в радианах. Если угол задан в градусах, используйте --theta-deg.");
    }

    std::vector<double> build_tau_q(int plate_count, int m_quad)
    {
        std::vector<double> values(plate_count * m_quad);
        for (int p = 0; p < plate_count; ++p)
        {
            for (int m = 0; m < m_quad; ++m)
                values[p * m_quad + m] = cos((2.0 * m + 1.0) / (2.0 * m_quad) * PI);
        }
        return values;
    }

    std::vector<double> build_tau_c(int plate_count, int n)
    {
        std::vector<double> values(plate_count * n);
        for (int p = 0; p < plate_count; ++p)
        {
            for (int i = 0; i < n; ++i)
                values[p * n + i] = cos((i + 0.5) / n * PI);
        }
        return values;
    }

    std::vector<double> build_t_q(const SolverParameters& params, const std::vector<double>& tau_q, int m_quad)
    {
        std::vector<double> values(params.plate_count * m_quad);
        for (int p = 0; p < params.plate_count; ++p)
        {
            for (int m = 0; m < m_quad; ++m)
                values[p * m_quad + m] = tau_to_x(params.alpha, params.beta, p, tau_q[p * m_quad + m]);
        }
        return values;
    }

    std::vector<double> build_w_q(const SolverParameters& params, int m_quad)
    {
        std::vector<double> values(params.plate_count * m_quad);
        for (int p = 0; p < params.plate_count; ++p)
        {
            double weight = PI / m_quad * half_length(params.alpha, params.beta, p);
            for (int m = 0; m < m_quad; ++m)
                values[p * m_quad + m] = weight;
        }
        return values;
    }

    std::vector<double> build_x_c(const SolverParameters& params, const std::vector<double>& tau_c)
    {
        std::vector<double> values(params.plate_count * params.n);
        for (int p = 0; p < params.plate_count; ++p)
        {
            for (int i = 0; i < params.n; ++i)
                values[p * params.n + i] = tau_to_x(params.alpha, params.beta, p, tau_c[p * params.n + i]);
        }
        return values;
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
        ComplexValue chi = params.skin_depth > 0.0
            ? ComplexValue(k_wave * params.skin_depth, k_wave * params.skin_depth)
            : ComplexValue();

        std::vector<double> tau_q = build_tau_q(params.plate_count, m_quad);
        std::vector<double> tau_c = build_tau_c(params.plate_count, params.n);
        std::vector<double> t_q = build_t_q(params, tau_q, m_quad);
        std::vector<double> w_q = build_w_q(params, m_quad);
        std::vector<double> x_c = build_x_c(params, tau_c);

        std::vector<cuDoubleComplex> solution(total_unknowns);

        auto total_start = std::chrono::high_resolution_clock::now();

        double *d_alpha = nullptr, *d_beta = nullptr, *d_tau_q = nullptr, *d_t_q = nullptr, *d_w_q = nullptr, *d_tau_c = nullptr, *d_x_c = nullptr;
        cuDoubleComplex* d_matrix = nullptr;
        cuDoubleComplex* d_rhs = nullptr;
        cuDoubleComplex* d_work = nullptr;
        int* d_pivots = nullptr;
        int* d_info = nullptr;
        cusolverDnHandle_t solver_handle = nullptr;

        check_cuda(cudaMalloc(&d_alpha, sizeof(double) * params.plate_count), "cudaMalloc(alpha)");
        check_cuda(cudaMalloc(&d_beta, sizeof(double) * params.plate_count), "cudaMalloc(beta)");
        check_cuda(cudaMalloc(&d_tau_q, sizeof(double) * tau_q.size()), "cudaMalloc(tau_q)");
        check_cuda(cudaMalloc(&d_t_q, sizeof(double) * t_q.size()), "cudaMalloc(t_q)");
        check_cuda(cudaMalloc(&d_w_q, sizeof(double) * w_q.size()), "cudaMalloc(w_q)");
        check_cuda(cudaMalloc(&d_tau_c, sizeof(double) * tau_c.size()), "cudaMalloc(tau_c)");
        check_cuda(cudaMalloc(&d_x_c, sizeof(double) * x_c.size()), "cudaMalloc(x_c)");
        check_cuda(cudaMalloc(&d_matrix, sizeof(cuDoubleComplex) * total_unknowns * total_unknowns), "cudaMalloc(matrix)");
        check_cuda(cudaMalloc(&d_rhs, sizeof(cuDoubleComplex) * total_unknowns), "cudaMalloc(rhs)");
        check_cuda(cudaMalloc(&d_pivots, sizeof(int) * total_unknowns), "cudaMalloc(pivots)");
        check_cuda(cudaMalloc(&d_info, sizeof(int)), "cudaMalloc(info)");

        check_cuda(cudaMemcpy(d_alpha, params.alpha, sizeof(double) * params.plate_count, cudaMemcpyHostToDevice), "cudaMemcpy(alpha)");
        check_cuda(cudaMemcpy(d_beta, params.beta, sizeof(double) * params.plate_count, cudaMemcpyHostToDevice), "cudaMemcpy(beta)");
        check_cuda(cudaMemcpy(d_tau_q, tau_q.data(), sizeof(double) * tau_q.size(), cudaMemcpyHostToDevice), "cudaMemcpy(tau_q)");
        check_cuda(cudaMemcpy(d_t_q, t_q.data(), sizeof(double) * t_q.size(), cudaMemcpyHostToDevice), "cudaMemcpy(t_q)");
        check_cuda(cudaMemcpy(d_w_q, w_q.data(), sizeof(double) * w_q.size(), cudaMemcpyHostToDevice), "cudaMemcpy(w_q)");
        check_cuda(cudaMemcpy(d_tau_c, tau_c.data(), sizeof(double) * tau_c.size(), cudaMemcpyHostToDevice), "cudaMemcpy(tau_c)");
        check_cuda(cudaMemcpy(d_x_c, x_c.data(), sizeof(double) * x_c.size(), cudaMemcpyHostToDevice), "cudaMemcpy(x_c)");

        cudaEvent_t assembly_start = nullptr;
        cudaEvent_t assembly_stop = nullptr;
        check_cuda(cudaEventCreate(&assembly_start), "cudaEventCreate(start)");
        check_cuda(cudaEventCreate(&assembly_stop), "cudaEventCreate(stop)");
        check_cuda(cudaEventRecord(assembly_start), "cudaEventRecord(start)");

        int matrix_threads = 256;
        int matrix_blocks = (total_unknowns * total_unknowns + matrix_threads - 1) / matrix_threads;
        assemble_matrix_kernel<<<matrix_blocks, matrix_threads>>>(
            d_matrix,
            d_alpha,
            d_beta,
            d_tau_q,
            d_t_q,
            d_w_q,
            d_tau_c,
            d_x_c,
            params.n,
            params.plate_count,
            m_quad,
            k_wave,
            chi);
        check_cuda(cudaGetLastError(), "assemble_matrix_kernel launch");

        int rhs_threads = 256;
        int rhs_blocks = (total_unknowns + rhs_threads - 1) / rhs_threads;
        assemble_rhs_kernel<<<rhs_blocks, rhs_threads>>>(
            d_rhs,
            d_x_c,
            params.n,
            params.plate_count,
            params.theta,
            k_wave,
            chi);
        check_cuda(cudaGetLastError(), "assemble_rhs_kernel launch");

        check_cuda(cudaEventRecord(assembly_stop), "cudaEventRecord(stop)");
        check_cuda(cudaEventSynchronize(assembly_stop), "cudaEventSynchronize(stop)");

        float assembly_ms = 0.0f;
        check_cuda(cudaEventElapsedTime(&assembly_ms, assembly_start, assembly_stop), "cudaEventElapsedTime");

        check_cusolver(cusolverDnCreate(&solver_handle), "cusolverDnCreate");
        int lwork = 0;
        check_cusolver(
            cusolverDnZgetrf_bufferSize(
                solver_handle,
                total_unknowns,
                total_unknowns,
                d_matrix,
                total_unknowns,
                &lwork),
            "cusolverDnZgetrf_bufferSize");
        check_cuda(cudaMalloc(&d_work, sizeof(cuDoubleComplex) * lwork), "cudaMalloc(work)");

        cudaEvent_t solve_start_event = nullptr;
        cudaEvent_t solve_stop_event = nullptr;
        check_cuda(cudaEventCreate(&solve_start_event), "cudaEventCreate(solve_start)");
        check_cuda(cudaEventCreate(&solve_stop_event), "cudaEventCreate(solve_stop)");
        check_cuda(cudaEventRecord(solve_start_event), "cudaEventRecord(solve_start)");
        check_cusolver(
            cusolverDnZgetrf(
                solver_handle,
                total_unknowns,
                total_unknowns,
                d_matrix,
                total_unknowns,
                d_work,
                d_pivots,
                d_info),
            "cusolverDnZgetrf");
        check_cusolver(
            cusolverDnZgetrs(
                solver_handle,
                CUBLAS_OP_N,
                total_unknowns,
                1,
                d_matrix,
                total_unknowns,
                d_pivots,
                d_rhs,
                total_unknowns,
                d_info),
            "cusolverDnZgetrs");
        check_cuda(cudaEventRecord(solve_stop_event), "cudaEventRecord(solve_stop)");
        check_cuda(cudaEventSynchronize(solve_stop_event), "cudaEventSynchronize(solve_stop)");
        auto total_stop = std::chrono::high_resolution_clock::now();

        int info_value = 0;
        check_cuda(cudaMemcpy(&info_value, d_info, sizeof(int), cudaMemcpyDeviceToHost), "cudaMemcpy(info)");
        if (info_value != 0)
        {
            std::ostringstream stream;
            stream << "cuSOLVER returned info=" << info_value;
            throw std::runtime_error(stream.str());
        }

        check_cuda(cudaMemcpy(solution.data(), d_rhs, sizeof(cuDoubleComplex) * total_unknowns, cudaMemcpyDeviceToHost), "cudaMemcpy(solution)");

        float solve_ms_gpu = 0.0f;
        check_cuda(cudaEventElapsedTime(&solve_ms_gpu, solve_start_event, solve_stop_event), "cudaEventElapsedTime(solve)");
        double solve_ms = static_cast<double>(solve_ms_gpu);
        double total_ms = std::chrono::duration<double, std::milli>(total_stop - total_start).count();

        cudaEventDestroy(assembly_start);
        cudaEventDestroy(assembly_stop);
        cudaEventDestroy(solve_start_event);
        cudaEventDestroy(solve_stop_event);
        if (solver_handle != nullptr) cusolverDnDestroy(solver_handle);
        cudaFree(d_alpha);
        cudaFree(d_beta);
        cudaFree(d_tau_q);
        cudaFree(d_t_q);
        cudaFree(d_w_q);
        cudaFree(d_tau_c);
        cudaFree(d_x_c);
        cudaFree(d_matrix);
        cudaFree(d_rhs);
        cudaFree(d_work);
        cudaFree(d_pivots);
        cudaFree(d_info);

        std::cout << std::setprecision(17);
        std::cout << "status=ok\n";
        std::cout << "backend=CUDA (matrix + solve)\n";
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
        std::cout << "assembly_ms=" << static_cast<double>(assembly_ms) << "\n";
        std::cout << "solve_ms=" << solve_ms << "\n";
        std::cout << "total_ms=" << total_ms << "\n";
        for (int i = 0; i < static_cast<int>(solution.size()); ++i)
            std::cout << "coeff_" << i << "=" << cuCreal(solution[i]) << "," << cuCimag(solution[i]) << "\n";
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "status=error\n";
        std::cerr << "message=" << ex.what() << "\n";
        return 1;
    }
}
