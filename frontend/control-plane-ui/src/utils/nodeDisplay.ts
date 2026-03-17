import type { NodeSummary } from '../types/dashboard'

export const PRIMARY_NODE_HOST = '5.61.37.29'

const genericNodeNamePattern = /^Amnezia Node \d+$/i

export function getNodeHost(node: Pick<NodeSummary, 'agentBaseAddress'>) {
  try {
    return new URL(node.agentBaseAddress).hostname
  } catch {
    return node.agentBaseAddress.replace(/^https?:\/\//i, '').split(':')[0] ?? node.agentBaseAddress
  }
}

export function isPrimaryNode(node: Pick<NodeSummary, 'agentBaseAddress' | 'agentIdentifier'>) {
  const host = getNodeHost(node)
  return host === PRIMARY_NODE_HOST || node.agentIdentifier === `amnezia-${PRIMARY_NODE_HOST.replaceAll('.', '-')}`
}

export function getNodeDisplayName(node: Pick<NodeSummary, 'name' | 'agentIdentifier' | 'agentBaseAddress'>) {
  const trimmedName = node.name.trim()
  const host = getNodeHost(node)

  if (trimmedName && !genericNodeNamePattern.test(trimmedName)) {
    return trimmedName
  }

  if (host) {
    return `Amnezia ${host}`
  }

  if (node.agentIdentifier.trim()) {
    return node.agentIdentifier.trim()
  }

  return trimmedName || 'Нода'
}

export function getNodeBadgeLabel(node: Pick<NodeSummary, 'agentBaseAddress' | 'agentIdentifier'>) {
  return isPrimaryNode(node) ? 'Основная' : null
}

export function sortNodesForDisplay(nodes: NodeSummary[]) {
  return nodes
    .map((node, index) => ({ node, index }))
    .sort((left, right) => {
      const leftPrimary = isPrimaryNode(left.node) ? 1 : 0
      const rightPrimary = isPrimaryNode(right.node) ? 1 : 0

      if (leftPrimary !== rightPrimary) {
        return rightPrimary - leftPrimary
      }

      return left.index - right.index
    })
    .map(({ node }) => node)
}
