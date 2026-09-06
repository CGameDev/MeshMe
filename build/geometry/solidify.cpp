// SPDX-License-Identifier: AGPL-3.0-only
// Thin adapter: libigl signed distance + Manifold level-set reconstruction.
// No upstream geometry implementation is modified.
#include <emscripten/bind.h>
#include <igl/signed_distance.h>
#include <manifold/manifold.h>
#include <cmath>
#include <stdexcept>

using emscripten::val;

val solidify(val positions, val indices, double edgeLength) {
  try {
    const auto p = emscripten::convertJSArrayToNumberVector<double>(positions);
    const auto f = emscripten::convertJSArrayToNumberVector<double>(indices);
    if (p.empty() || p.size() % 3 || f.empty() || f.size() % 3 ||
        p.size() > 3000000 || f.size() > 3000000)
      throw std::runtime_error("Expected a mesh with at most one million vertices and triangles.");
    if (!std::isfinite(edgeLength) || edgeLength <= 0)
      throw std::runtime_error("Resolution must be a positive finite length in millimetres.");
    Eigen::MatrixXd V(p.size() / 3, 3);
    Eigen::MatrixXi F(f.size() / 3, 3);
    for (size_t i = 0; i < p.size(); ++i) {
      if (!std::isfinite(p[i])) throw std::runtime_error("Repair non-finite vertices first.");
      V(i / 3, i % 3) = p[i];
    }
    for (size_t i = 0; i < f.size(); ++i) {
      if (!std::isfinite(f[i]) || f[i] < 0 || f[i] >= V.rows() || std::floor(f[i]) != f[i])
        throw std::runtime_error("Repair invalid triangle indices first.");
      F(i / 3, i % 3) = static_cast<int>(f[i]);
    }
    for (int i = 0; i < F.rows(); ++i) {
      const Eigen::Vector3d a = V.row(F(i, 1)) - V.row(F(i, 0));
      const Eigen::Vector3d b = V.row(F(i, 2)) - V.row(F(i, 0));
      if (a.cross(b).squaredNorm() == 0) throw std::runtime_error("Repair zero-area triangles first.");
    }
    const Eigen::RowVector3d lo = V.colwise().minCoeff(), hi = V.colwise().maxCoeff();
    double samples = 1;
    for (int axis = 0; axis < 3; ++axis) samples *= std::ceil((hi[axis] - lo[axis]) / edgeLength) + 5;
    if (!std::isfinite(samples) || samples > 2000000)
      throw std::runtime_error("Resolution exceeds the two-million-cell preview budget. Increase the cell size.");
    igl::AABB<Eigen::MatrixXd, 3> tree;
    tree.init(V, F);
    igl::WindingNumberAABB<double, int> hierarchy;
    hierarchy.set_mesh(V, F);
    hierarchy.grow();
    const auto distance = [&](manifold::vec3 point) {
      const Eigen::RowVector3d q(point.x, point.y, point.z);
      // libigl is negative inside; Manifold's LevelSet is positive inside.
      return -igl::signed_distance_winding_number(tree, V, F, hierarchy, q);
    };
    const manifold::Box bounds(
      {lo[0] - 2 * edgeLength, lo[1] - 2 * edgeLength, lo[2] - 2 * edgeLength},
      {hi[0] + 2 * edgeLength, hi[1] + 2 * edgeLength, hi[2] + 2 * edgeLength});
    const auto result = manifold::Manifold::LevelSet(distance, bounds, edgeLength, 0, -1, false);
    if (result.Status() != manifold::Manifold::Error::NoError || result.IsEmpty() || result.Volume() <= 0)
      throw std::runtime_error("No enclosed solid at this resolution. Check winding/openings or reduce the cell size.");
    if (result.NumTri() > 2000000) throw std::runtime_error("Result exceeds the two-million-triangle preview budget.");
    const auto mesh = result.GetMeshGL();
    val output = val::object();
    output.set("positions", val::global("Float32Array").new_(val(emscripten::typed_memory_view(mesh.vertProperties.size(), mesh.vertProperties.data()))));
    output.set("indices", val::global("Uint32Array").new_(val(emscripten::typed_memory_view(mesh.triVerts.size(), mesh.triVerts.data()))));
    output.set("volume", result.Volume());
    output.set("triangles", result.NumTri());
    return output;
  } catch (const std::exception& error) {
    val output = val::object();
    output.set("error", std::string(error.what()));
    return output;
  }
}

EMSCRIPTEN_BINDINGS(meshme_solidify) {
  emscripten::function("solidify", &solidify);
}
